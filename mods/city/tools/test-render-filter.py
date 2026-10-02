#!/usr/bin/env python3
"""Render real combined-shader regressions in a headless EGL pbuffer.

Requirements: Python 3, numpy, PyOpenGL, and an EGL driver supporting desktop GL
or GLES 3. No game build or desktop display is needed. Run from any directory:

    python mods/city/tools/test-render-filter.py       # GLSL 140
    python mods/city/tools/test-render-filter.py es    # GLSL 300 es

A failed compile, link, or image assertion exits nonzero. Tests cover native and
integer scaling, fractional/mirrored/rotated filtering, palette lookup order,
minification, atlas isolation, and premultiplied transparent edges.
"""
import os
os.environ['PYOPENGL_PLATFORM'] = 'egl'
os.environ['EGL_PLATFORM'] = 'surfaceless'
import sys, ctypes
from pathlib import Path
import numpy as np
from OpenGL import EGL, GL
from OpenGL.GL.shaders import compileShader, compileProgram
embedded = len(sys.argv) > 1 and sys.argv[1] == 'es'
display = EGL.eglGetDisplay(EGL.EGL_DEFAULT_DISPLAY)
major, minor = (ctypes.c_int(), ctypes.c_int())
EGL.eglInitialize(display, major, minor)
EGL.eglBindAPI(EGL.EGL_OPENGL_ES_API if embedded else EGL.EGL_OPENGL_API)
attrs = (ctypes.c_int * 13)(EGL.EGL_SURFACE_TYPE, EGL.EGL_PBUFFER_BIT, EGL.EGL_RENDERABLE_TYPE, 64 if embedded else EGL.EGL_OPENGL_BIT, EGL.EGL_RED_SIZE, 8, EGL.EGL_GREEN_SIZE, 8, EGL.EGL_BLUE_SIZE, 8, EGL.EGL_ALPHA_SIZE, 8, EGL.EGL_NONE)
configs = (EGL.EGLConfig * 1)()
count = ctypes.c_int()
EGL.eglChooseConfig(display, attrs, configs, 1, count)
ctxattrs = (ctypes.c_int * 3)(EGL.EGL_CONTEXT_CLIENT_VERSION, 3, EGL.EGL_NONE) if embedded else None
context = EGL.eglCreateContext(display, configs[0], EGL.EGL_NO_CONTEXT, ctxattrs)
pattributes = (ctypes.c_int * 5)(EGL.EGL_WIDTH, 64, EGL.EGL_HEIGHT, 64, EGL.EGL_NONE)
surface = EGL.eglCreatePbufferSurface(display, configs[0], pattributes)
EGL.eglMakeCurrent(display, surface, surface, context)
version = '300 es' if embedded else '140'
root = Path(__file__).resolve().parents[3]
program = compileProgram(*[compileShader((root / ('glsl/combined.' + ext)).read_text().replace('{VERSION}', version), kind) for ext, kind in [('vert', GL.GL_VERTEX_SHADER), ('frag', GL.GL_FRAGMENT_SHADER)]], validate=False)
GL.glUseProgram(program)
print('Driver:', GL.glGetString(GL.GL_VERSION).decode(), GL.glGetString(GL.GL_RENDERER).decode())
GL.glBindVertexArray(GL.glGenVertexArrays(1))
vbo = GL.glGenBuffers(1)
GL.glBindBuffer(GL.GL_ARRAY_BUFFER, vbo)
dtype = np.dtype([('xyz', 'f4', 3), ('uv', 'f4', 4), ('flags', 'u4'), ('tint', 'f4', 4), ('bounds', 'f4', 4)])
assert dtype.itemsize == 64
for name, components, offset in [('aVertexPosition', 3, 0), ('aVertexTexCoord', 4, 12), ('aVertexAttributes', 1, 28), ('aVertexTint', 4, 32), ('aVertexTextureBounds', 4, 48)]:
    loc = GL.glGetAttribLocation(program, name)
    GL.glEnableVertexAttribArray(loc)
    if name == 'aVertexAttributes':
        GL.glVertexAttribIPointer(loc, components, GL.GL_UNSIGNED_INT, 64, ctypes.c_void_p(offset))
    else:
        GL.glVertexAttribPointer(loc, components, GL.GL_FLOAT, False, 64, ctypes.c_void_p(offset))
textures = GL.glGenTextures(3)

def texture(unit, data, linear=False):
    GL.glActiveTexture(GL.GL_TEXTURE0 + unit)
    GL.glBindTexture(GL.GL_TEXTURE_2D, int(textures[unit]))
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MIN_FILTER, GL.GL_LINEAR if linear else GL.GL_NEAREST)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_MAG_FILTER, GL.GL_LINEAR if linear else GL.GL_NEAREST)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_S, GL.GL_CLAMP_TO_EDGE)
    GL.glTexParameteri(GL.GL_TEXTURE_2D, GL.GL_TEXTURE_WRAP_T, GL.GL_CLAMP_TO_EDGE)
    GL.glTexImage2D(GL.GL_TEXTURE_2D, 0, GL.GL_RGBA8, data.shape[1], data.shape[0], 0, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE, data)
    GL.glUniform1i(GL.glGetUniformLocation(program, ['Texture0', 'Palette', 'ColorShifts'][unit]), unit)
GL.glUniform3f(GL.glGetUniformLocation(program, 'Scroll'), 0, 0, 0)
GL.glUniform1f(GL.glGetUniformLocation(program, 'PaletteRows'), 1)
GL.glUniform1i(GL.glGetUniformLocation(program, 'EnableDepthPreview'), False)
texture(2, np.zeros((1, 2, 4), dtype=np.uint8))
# Poison outside the tested sprite must never leak into its filtered samples.
rgba = np.empty((32, 32, 4), dtype=np.uint8)
rgba[:] = [255, 0, 255, 255]
indices = np.empty_like(rgba)
indices[:] = [128, 0, 0, 255]
palette = np.empty((1, 256, 4), dtype=np.uint8)
palette[:] = [255, 0, 255, 255]
palette[0, 1] = [255, 255, 255, 255]
palette[0, 255] = [0, 0, 0, 255]
for y in range(8):
    for x in range(8):
        white = (x + y) % 2 == 0
        rgba[2 + y, 2 + x] = [255 if white else 0] * 3 + [255]
        indices[2 + y, 2 + x] = [1 if white else 255, 0, 0, 255]
texture(1, palette)

def render(data, width, height=None, paletted=False, hardware=False, mirror=False, rotate=False, enabled=True):
    height = height or width
    texture(0, data, hardware)
    GL.glViewport(0, 0, width, height)
    GL.glClearColor(0, 0, 0, 0)
    GL.glClear(GL.GL_COLOR_BUFFER_BIT)
    GL.glUniform3f(GL.glGetUniformLocation(program, 'p1'), 2 / width, 2 / height, 1)
    GL.glUniform3f(GL.glGetUniformLocation(program, 'p2'), -1, -1, 0)
    GL.glUniform1i(GL.glGetUniformLocation(program, 'EnablePixelArtScaling'), enabled)
    v = np.zeros(6, dtype=dtype)
    uv = np.array([[2, 2], [10, 2], [10, 10], [2, 10]], dtype=np.float32)
    # Match Sprite's 1/128-texel inset.
    uv = np.where(uv == 2, uv + 1 / 128, uv - 1 / 128) / 32
    if mirror:
        uv = uv[[1, 0, 3, 2]]
    if rotate:
        uv = uv[[3, 0, 1, 2]]
    order = [0, 1, 2, 2, 3, 0]
    v['xyz'] = np.array([[0, 0, 0], [width, 0, 0], [width, height, 0], [0, height, 0]], dtype=np.float32)[order]
    v['uv'][:, :2] = uv[order]
    v['flags'] = 1 if paletted else 2
    if hardware:
        v['flags'] |= 1 << 12
    v['tint'] = 1
    v['bounds'] = [(2 + 1 / 128) / 32, (2 + 1 / 128) / 32, (10 - 1 / 128) / 32, (10 - 1 / 128) / 32]
    GL.glBufferData(GL.GL_ARRAY_BUFFER, v.nbytes, v, GL.GL_STREAM_DRAW)
    GL.glDrawArrays(GL.GL_TRIANGLES, 0, 6)
    return np.frombuffer(GL.glReadPixels(0, 0, width, height, GL.GL_RGBA, GL.GL_UNSIGNED_BYTE), dtype=np.uint8).reshape(height, width, 4).copy()
checks = 0

def check(condition, name):
    global checks
    assert condition, name
    checks += 1
    print('PASS', name)
for width in (8, 16, 24):
    r = render(rgba, width)
    check(set(np.unique(r[:, :, :3])) == {0, 255}, f'aligned integer scale {width / 8:g} stays crisp')
    check(np.max(np.abs(r.astype(int) - render(rgba, width, enabled=False).astype(int))) <= 1, f'native/AA parity scale {width / 8:g}')
for width in (4, 5, 12, 15):
    r = render(rgba, width)
    p = render(indices, width, paletted=True)
    check(np.max(np.abs(r.astype(int) - p.astype(int))) <= 1, f'RGBA/palette filtering parity {width}/8')
    h = render(rgba, width, hardware=True)
    check(np.max(np.abs(r.astype(int) - h.astype(int))) <= 1, f'hardware/manual bilinear parity {width}/8')
    check(np.max(np.abs(r.astype(int) - render(rgba, width, mirror=True)[:, ::-1].astype(int))) <= 1, f'mirrored scaling parity {width}/8')
    check(np.max(np.abs(r.astype(int) - np.rot90(render(rgba, width, rotate=True), -1).astype(int))) <= 1, f'90-degree rotation parity {width}/8')
    check(np.array_equal(r[:, :, 0], r[:, :, 1]) and np.array_equal(r[:, :, 1], r[:, :, 2]), f'atlas poison exclusion {width}/8')
check(np.max(np.abs(render(rgba, 4)[:, :, :3].astype(int) - 128)) <= 1, '2:1 checkerboard minification averages instead of aliasing')
check(len(np.unique(render(rgba, 12)[:, :, 0])) > 2, 'fractional magnification has antialiased transitions')
# Premultiplied RGB at a transparent boundary must never exceed its coverage.
alpha = rgba.copy()
alpha[2:10, 2:6] = [0, 0, 0, 0]
alpha[2:10, 6:10] = [0, 255, 255, 255]
a = render(alpha, 12)
check(np.all(a[:, :, 0] == 0) and np.max(np.abs(a[:, :, 1].astype(int) - a[:, :, 3].astype(int))) <= 1, 'transparent edges retain premultiplied coverage without halos')
print(f'PASS {checks} rendered checks under GLSL {version}')
