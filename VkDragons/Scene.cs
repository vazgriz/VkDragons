using System;
using System.Numerics;
using System.Collections.Generic;

using CSGL;
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
        DescriptorSetLayout uniformSetLayout;
        DescriptorSetLayout textureSetLayout;
        DescriptorSetLayout modelSetLayout;

        UniformBuffer camUniform;
        UniformBuffer lightUniform;

        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public double Time { get; private set; }

        public Scene(Window window) {
            renderer = new Renderer(window);
            camera = new Camera(45, window.FramebufferWidth, window.FramebufferHeight);
            input = new Input(window, this, renderer, camera);

            Width = (uint)window.FramebufferWidth;
            Height = (uint)window.FramebufferHeight;

            camera.Position = new Vector3(0, 0, 1);

            CreateSampler();
            CreateUniformSetLayout();
            CreateTextureSetLayout();
            CreateModelSetLayout();

            camUniform = new UniformBuffer(renderer, (ulong)Interop.SizeOf<CameraUniform>(), uniformSetLayout);
            lightUniform = new UniformBuffer(renderer, (ulong)Interop.SizeOf<LightUniform>(), uniformSetLayout);
        }

        public void Dispose() {
            sampler.Dispose();
            uniformSetLayout.Dispose();
            textureSetLayout.Dispose();
            modelSetLayout.Dispose();
            camUniform.Dispose();
            lightUniform.Dispose();
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

        void CreateUniformSetLayout() {
            DescriptorSetLayoutCreateInfo info = new DescriptorSetLayoutCreateInfo {
                bindings = new List<VkDescriptorSetLayoutBinding> {
                    new VkDescriptorSetLayoutBinding {
                        binding = 0,
                        descriptorType = VkDescriptorType.UniformBuffer,
                        descriptorCount = 1,
                        stageFlags = VkShaderStageFlags.VertexBit | VkShaderStageFlags.FragmentBit
                    }
                }
            };

            uniformSetLayout = new DescriptorSetLayout(renderer.Device, info);
        }

        void CreateTextureSetLayout() {
            DescriptorSetLayoutCreateInfo info = new DescriptorSetLayoutCreateInfo {
                bindings = new List<VkDescriptorSetLayoutBinding> {
                    new VkDescriptorSetLayoutBinding {
                        binding = 0,
                        descriptorType = VkDescriptorType.CombinedImageSampler,
                        descriptorCount = 1,
                        stageFlags = VkShaderStageFlags.FragmentBit
                    }
                }
            };

            textureSetLayout = new DescriptorSetLayout(renderer.Device, info);
        }

        void CreateModelSetLayout() {
            List<VkDescriptorSetLayoutBinding> bindings = new List<VkDescriptorSetLayoutBinding>(6);

            for (int i = 0; i < 6; i++) {
                bindings.Add(new VkDescriptorSetLayoutBinding {
                    binding = (uint)i,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    descriptorCount = 1,
                    stageFlags = VkShaderStageFlags.FragmentBit
                });
            }

            DescriptorSetLayoutCreateInfo info = new DescriptorSetLayoutCreateInfo {
                bindings = bindings
            };

            modelSetLayout = new DescriptorSetLayout(renderer.Device, info);
        }
    }
}
