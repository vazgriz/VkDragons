#version 450
#extension GL_ARB_separate_shader_objects : enable

// Input: position in model space
layout(location = 0) in vec3 position; 

layout(set = 1, binding = 0) uniform samplerCube textureCubeMap;

// Output: the fragment color
layout(location = 0) out vec4 fragColor;

void main(){
	vec3 uvw = vec3(position.x, -position.y, position.z);
	fragColor = texture(textureCubeMap, uvw);
}
