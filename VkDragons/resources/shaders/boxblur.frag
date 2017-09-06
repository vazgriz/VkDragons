#version 450
#extension GL_ARB_separate_shader_objects : enable

// Input: UV coordinates
layout(location = 0) in vec2 uv;

// Uniforms: the texture
layout(set = 0, binding = 0) uniform sampler2D screenTexture;

// Output: the fragment color
layout(location = 0) out vec2 fragColor;

void main(){	
	// We have to unroll the box blur loop manually.
	
	vec2 color = vec2(0.0);
	
	color += textureOffset(screenTexture, uv, ivec2(-2,-2)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-2,-1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-2, 0)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-2, 1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-2, 2)).rg;
	
	color += textureOffset(screenTexture, uv, ivec2(-1,-2)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-1,-1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-1, 0)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-1, 1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(-1, 2)).rg;
	
	color += textureOffset(screenTexture, uv, ivec2(0,-2)).rg;
	color += textureOffset(screenTexture, uv, ivec2(0,-1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(0, 0)).rg;
	color += textureOffset(screenTexture, uv, ivec2(0, 1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(0, 2)).rg;
	
	color += textureOffset(screenTexture, uv, ivec2(1,-2)).rg;
	color += textureOffset(screenTexture, uv, ivec2(1,-1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(1, 0)).rg;
	color += textureOffset(screenTexture, uv, ivec2(1, 1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(1, 2)).rg;
	
	color += textureOffset(screenTexture, uv, ivec2(2,-2)).rg;
	color += textureOffset(screenTexture, uv, ivec2(2,-1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(2, 0)).rg;
	color += textureOffset(screenTexture, uv, ivec2(2, 1)).rg;
	color += textureOffset(screenTexture, uv, ivec2(2, 2)).rg;
	
	fragColor = color / 25.0;
}
