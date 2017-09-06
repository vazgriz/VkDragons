using System;
using System.Numerics;

using CSGL.GLFW;
using CSGL.Vulkan;

namespace VkDragons {
    struct CameraUniform {
        public Matrix4x4 camProjection;
        public Matrix4x4 camView;
        public Matrix4x4 camRotationOnlyView;
        public Matrix4x4 camViewInverse;
    }

    struct LightUniform {
        public Matrix4x4 lightProjection;
        public Matrix4x4 lightView;
        public Vector4 lightPosition;
        public Vector4 lightIa;
        public Vector4 lightId;
        public Vector4 lightIs;
        public float lightShininess;
    }

    public class Scene : IDisposable {
        Renderer renderer;
        Camera camera;
        Input input;

        Sampler sampler;

        public Scene(Window window) {
            renderer = new Renderer(window);
            camera = new Camera(45, window.FramebufferWidth, window.FramebufferHeight);
            input = new Input(window, this, renderer, camera);

            CreateSampler();
        }

        public void Dispose() {
            sampler.Dispose();
            renderer.Dispose();
        }

        public void Resize(int width, int height) {
            renderer.Resize(width, height);
        }

        void CreateSampler() {
            SamplerCreateInfo info = new SamplerCreateInfo {
                magFilter = VkFilter.Linear,
                minFilter = VkFilter.Linear,
                addressModeU = VkSamplerAddressMode.ClampToEdge,
                addressModeV = VkSamplerAddressMode.ClampToEdge,
                addressModeW = VkSamplerAddressMode.ClampToEdge,
                maxAnisotropy = 1,
                borderColor = VkBorderColor.IntOpaqueBlack,
                mipmapMode = VkSamplerMipmapMode.Linear,
                maxLod = 8f
            };

            sampler = new Sampler(renderer.Device, info);
        }
    }
}
