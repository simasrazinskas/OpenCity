#version {VERSION}
#ifdef GL_ES
precision highp float;
precision highp int;
#endif

uniform sampler2D Texture0;
uniform sampler2D Texture1;
uniform sampler2D Texture2;
uniform sampler2D Texture3;
uniform sampler2D Texture4;
uniform sampler2D Texture5;
uniform sampler2D Texture6;
uniform sampler2D Texture7;
uniform sampler2D Palette;
uniform sampler2D ColorShifts;

uniform bool EnableDepthPreview;
uniform vec2 DepthPreviewParams;
uniform float DepthTextureScale;
uniform bool EnablePixelArtScaling;

in vec4 vTexCoord;
flat in float vTexPalette;
flat in vec4 vChannelMask;
flat in uint vChannelSampler;
flat in uint vChannelType;
flat in vec4 vDepthMask;
flat in uint vDepthSampler;
in vec4 vTint;
flat in vec4 vTextureBounds;
flat in uint vHardwareBilinearFiltering;

out vec4 fragColor;

vec3 rgb2hsv(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(0.0, -1.0 / 3.0, 2.0 / 3.0, -1.0);
	vec4 p = c.g < c.b ? vec4(c.bg, K.wz) : vec4(c.gb, K.xy);
	vec4 q = c.r < p.x ? vec4(p.xyw, c.r) : vec4(c.r, p.yzx);
	float d = q.x - min(q.w, q.y);
	float e = 1.0e-10;
	return vec3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
}

vec3 hsv2rgb(vec3 c)
{
	// From http://lolengine.net/blog/2013/07/27/rgb-to-hsv-in-glsl
	vec4 K = vec4(1.0, 2.0 / 3.0, 1.0 / 3.0, 3.0);
	vec3 p = abs(fract(c.xxx + K.xyz) * 6.0 - K.www);
	return c.z * mix(K.xxx, clamp(p - K.xxx, 0.0, 1.0), c.y);
}

float srgb2linear(float c)
{
	// Standard gamma conversion equation: see e.g. https://entropymine.com/imageworsener/srgbformula/
	return c <= 0.04045f ? c / 12.92f : pow((c + 0.055f) / 1.055f, 2.4f);
}

vec4 srgb2linear(vec4 c)
{
	// The SRGB color has pre-multiplied alpha which we must undo before removing the the gamma correction
	return c.a * vec4(srgb2linear(c.r / c.a), srgb2linear(c.g / c.a), srgb2linear(c.b / c.a), 1.0f);
}

float linear2srgb(float c)
{
	// Standard gamma conversion equation: see e.g. https://entropymine.com/imageworsener/srgbformula/
	return c <= 0.0031308 ? c * 12.92f : 1.055f * pow(c, 1.0f / 2.4f) - 0.055f;
}

vec4 linear2srgb(vec4 c)
{
	// The linear color has pre-multiplied alpha which we must undo before applying the the gamma correction
	return c.a * vec4(linear2srgb(c.r / c.a), linear2srgb(c.g / c.a), linear2srgb(c.b / c.a), 1.0f);
}

vec2 Size(uint samplerIndex)
{
	switch (samplerIndex)
	{
		case 7u:
			return vec2(textureSize(Texture7, 0));
		case 6u:
			return vec2(textureSize(Texture6, 0));
		case 5u:
			return vec2(textureSize(Texture5, 0));
		case 4u:
			return vec2(textureSize(Texture4, 0));
		case 3u:
			return vec2(textureSize(Texture3, 0));
		case 2u:
			return vec2(textureSize(Texture2, 0));
		case 1u:
			return vec2(textureSize(Texture1, 0));
		default:
			return vec2(textureSize(Texture0, 0));
	}
}

vec4 Sample(uint samplerIndex, vec2 pos)
{
	switch (samplerIndex)
	{
		case 7u:
			return texture(Texture7, pos);
		case 6u:
			return texture(Texture6, pos);
		case 5u:
			return texture(Texture5, pos);
		case 4u:
			return texture(Texture4, pos);
		case 3u:
			return texture(Texture3, pos);
		case 2u:
			return texture(Texture2, pos);
		case 1u:
			return texture(Texture1, pos);
		default:
			return texture(Texture0, pos);
	}
}

vec4 ResolveColor(vec4 sampleColor)
{
	if ((vChannelType & 0x01u) != 0u)
		return texture(Palette, vec2(dot(sampleColor, vChannelMask), vTexPalette));

	return sampleColor;
}

vec4 SampleBilinear(uint samplerIndex, vec2 coords, vec2 textureSize, vec4 bounds)
{
	// Clamp to the first/last texel centers of this sprite, never another sprite on the sheet.
	coords = clamp(coords, bounds.xy, bounds.zw);
	if (vHardwareBilinearFiltering != 0u && (vChannelType & 0x01u) == 0u)
		return Sample(samplerIndex, coords);

	vec2 texPos = coords * textureSize - vec2(0.5);
	vec2 interp = fract(texPos);
	vec2 tl = (floor(texPos) + vec2(0.5)) / textureSize;
	vec2 px = 1.0 / textureSize;

	// Resolve palette indices BEFORE blending. The same path also filters nearest-sampled RGBA sheets.
	vec4 c1 = ResolveColor(Sample(samplerIndex, clamp(tl, bounds.xy, bounds.zw)));
	vec4 c2 = ResolveColor(Sample(samplerIndex, clamp(tl + vec2(px.x, 0.), bounds.xy, bounds.zw)));
	vec4 c3 = ResolveColor(Sample(samplerIndex, clamp(tl + vec2(0., px.y), bounds.xy, bounds.zw)));
	vec4 c4 = ResolveColor(Sample(samplerIndex, clamp(tl + px, bounds.xy, bounds.zw)));

	// Sheet colours and palette colours are premultiplied, preventing transparent edge halos.
	return mix(mix(c1, c2, interp.x), mix(c3, c4, interp.x), interp.y);
}

vec4 ColorShift(vec4 c, float p)
{
	vec4 range = texture(ColorShifts, vec2(0.25, p));
 	vec4 shift = texture(ColorShifts, vec2(0.75, p));

	vec3 hsv = rgb2hsv(srgb2linear(c).rgb);
	if (hsv.r > range.r && range.g >= hsv.r)
		c = linear2srgb(vec4(hsv2rgb(vec3(hsv.r + shift.r, clamp(hsv.g + shift.g, 0.0, 1.0), hsv.b * clamp(shift.b, 0.0, 1.0))), c.a));

	return c;
}

void main()
{
	vec2 coords = vTexCoord.st;
	bool isPaletted = (vChannelType & 0x01u) != 0u;
	bool isColor = vChannelType == 0u;

	vec4 c;
	if (isColor)
		c = vTexCoord;
	else if (EnablePixelArtScaling)
	{
		vec2 textureSize = Size(vChannelSampler);
		vec4 texelBounds = floor(vTextureBounds * textureSize.xyxy);
		// Sprite UVs are slightly inset to protect nearest sampling. Undo that inset for the
		// filtered path so native/integer scales land exactly on texel centers.
		vec2 texelCoords = texelBounds.xy + (coords - vTextureBounds.xy)
			/ max(vTextureBounds.zw - vTextureBounds.xy, vec2(0.000001))
			* (texelBounds.zw + vec2(1.0) - texelBounds.xy);
		coords = texelCoords / textureSize;
		vec2 dx = dFdx(texelCoords);
		vec2 dy = dFdy(texelCoords);

		// Both screen derivatives contribute to each texture axis: signed diagonal derivatives
		// break mirrored sprites and become zero at 90 degree rotations.
		vec2 footprint = vec2(length(vec2(dx.x, dy.x)), length(vec2(dx.y, dy.y)));
		vec4 bounds = (texelBounds + vec4(0.5)) / textureSize.xyxy;
		if (any(greaterThan(footprint, vec2(1.001))))
		{
			// Average bilinear taps over the screen pixel footprint, for RGBA AND palette sprites.
			// Derivative vectors keep the sample grid aligned with the image even when rotated.
			vec2 qx = 0.25 * dx / textureSize;
			vec2 qy = 0.25 * dy / textureSize;
			c = 0.25 * (SampleBilinear(vChannelSampler, coords - qx - qy, textureSize, bounds)
				+ SampleBilinear(vChannelSampler, coords + qx - qy, textureSize, bounds)
				+ SampleBilinear(vChannelSampler, coords - qx + qy, textureSize, bounds)
				+ SampleBilinear(vChannelSampler, coords + qx + qy, textureSize, bounds));
		}
		else
		{
			// Sharp bilinear: keep texel interiors crisp, with a one-screen-pixel transition
			// at their boundaries. Whole, aligned zoom levels still sample texel centers.
			vec2 pixelsPerTexel = 1.0 / max(footprint, vec2(0.0001));
			vec2 offset = fract(texelCoords);
			vec2 interp = clamp(offset * pixelsPerTexel, 0.0, 0.5)
				+ clamp((offset - 1.0) * pixelsPerTexel + 0.5, 0.0, 0.5);
			coords = (floor(texelCoords) + interp) / textureSize;
			c = SampleBilinear(vChannelSampler, coords, textureSize, bounds);
		}
	}
	else
		c = ResolveColor(Sample(vChannelSampler, coords));

	// Discard any transparent fragments (both color and depth)
	if (c.a == 0.0)
		discard;

	if (!isPaletted && vTexPalette > 0.0)
		c = ColorShift(c, vTexPalette);

	float depth = gl_FragCoord.z;
	if (length(vDepthMask) > 0.0)
	{
		vec4 y = Sample(vDepthSampler, vTexCoord.pq);
		depth = depth + DepthTextureScale * dot(y, vDepthMask);
	}

	gl_FragDepth = depth;

	if (EnableDepthPreview)
	{
		float intensity = 1.0 - clamp(DepthPreviewParams.x * depth - 0.5 * DepthPreviewParams.x - DepthPreviewParams.y + 0.5, 0.0, 1.0);
		fragColor = vec4(vec3(intensity), 1.0);
	}
	else
	{
		// A negative tint alpha indicates that the tint should replace the colour instead of multiplying it
		if (vTint.a < 0.0)
			c = vec4(vTint.rgb, -vTint.a);
		else
			c *= vTint;

		fragColor = c;
	}
}
