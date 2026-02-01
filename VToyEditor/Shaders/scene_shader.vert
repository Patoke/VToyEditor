#version 330 core

layout (location = 0) in vec3 aPos;
layout (location = 1) in vec3 aNormal;
layout (location = 2) in vec2 aTex;

uniform mat4 model;
uniform mat4 view;
uniform mat4 projection;
uniform vec3 viewPos;

struct Light {
    int type;      
    vec3 position;
    vec3 direction;
    float range;
    float att0, att1, att2;
    float theta, phi, falloff;
    vec4 ambient, diffuse, specular;
};

#define MAX_LIGHTS 128
uniform Light lights[MAX_LIGHTS];
uniform int numLights;
uniform vec3 globalAmbient;
uniform bool uIsUnlit = false;

out vec2 TexCoord;
out vec3 LightingFactor; // Ambient + diffuse
out vec3 Specular;
out float FogDepth;

void main() {
    vec4 worldPos = model * vec4(aPos, 1.0);
    gl_Position = projection * view * worldPos;

    FogDepth = abs(vec4(view * worldPos).z);
    TexCoord = aTex;

    if (uIsUnlit) {
        LightingFactor = vec3(1.0);
        Specular = vec3(0.0);
        return;
    }

    vec3 FragPos = worldPos.xyz;
    vec3 N = normalize(mat3(transpose(inverse(model))) * aNormal);
    vec3 V = normalize(viewPos - FragPos);

    // Reimplementation of D3D8/D3D9s hardware lighting
    vec3 ambientAccum = globalAmbient;
    vec3 diffuseAccum = vec3(0.0);
    vec3 specularAccum = vec3(0.0);

    for(int i = 0; i < numLights; i++) {
        Light light = lights[i];
        vec3 L;
        float atten = 1.0;
        float rho = 1.0;

        // Directional
        if(light.type == 2) { 
            L = normalize(-light.direction);
        } 
        // Point or spot
        else {
            vec3 lightVec = light.position - FragPos;
            float d = length(lightVec);
            L = normalize(lightVec);

            if(d >= light.range) continue;

            atten = 1.0 / (light.att0 + (light.att1 * d) + (light.att2 * d * d) + 0.000001);

            if(light.type == 1) { // Spot
                float cosAlpha = dot(L, normalize(-light.direction));
                float cosTheta = cos(light.theta * 0.5);
                float cosPhi = cos(light.phi * 0.5);
                
                if(cosAlpha <= cosPhi) rho = 0.0;
                else if(cosAlpha > cosTheta) rho = 1.0;
                else rho = pow((cosAlpha - cosPhi) / (cosTheta - cosPhi), light.falloff);
            }
        }

        float factor = atten * rho;
        float nDotL = max(dot(N, L), 0.0);
        
        ambientAccum += light.ambient.rgb * factor;
        diffuseAccum += light.diffuse.rgb * nDotL * factor;

        if(nDotL > 0.0) {
            vec3 H = normalize(L + V);
            float nDotH = max(dot(N, H), 0.0);
            // 32 is the standard D3D8/D3D9 shininess if not specified by material
            specularAccum += light.specular.rgb * pow(nDotH, 32.0) * factor;
        }
    }

    LightingFactor = clamp(ambientAccum + diffuseAccum, 0.0, 1.0);
    Specular = specularAccum;
}