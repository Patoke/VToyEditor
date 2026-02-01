#version 330 core

out vec4 FragColor;

in vec2 TexCoord;
in vec3 LightingFactor;
in vec3 Specular;
in float FogDepth; 

uniform vec3 uFogColor;
uniform float uFogNear;
uniform float uFogFar;
uniform sampler2D uTexture0;

uniform float alphaCutoff;
uniform float uGamma = 1.0;

void main() {
    vec4 texColor = texture(uTexture0, TexCoord);
    if(texColor.a < alphaCutoff) discard;

    // Apply lighting
    vec3 finalRGB = (texColor.rgb * LightingFactor) + Specular;
    finalRGB = clamp(finalRGB, 0.0, 1.0);

    // Game uses linear fog https://learn.microsoft.com/en-us/windows/win32/direct3d9/fog-formulas
    float fogFactor = (uFogFar - FogDepth) / (uFogFar - uFogNear);
    fogFactor = clamp(fogFactor, 0.0, 1.0);

    finalRGB = mix(uFogColor, finalRGB, fogFactor);

    // Gamma correction
    if (uGamma != 1.0 && uGamma > 0.0) {
        finalRGB = pow(finalRGB, vec3(1.0 / uGamma));
    }

    FragColor = vec4(finalRGB, texColor.a);
}