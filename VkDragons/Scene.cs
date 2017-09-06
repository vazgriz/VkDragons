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

        Model dragon;
        Model suzanne;
        Model plane;

        Material dragonMat;
        Material suzanneMat;
        Material planeMat;

        DisposableList<Texture> textures;

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

            dragon = new Model(renderer, "resources/dragon.obj");
            suzanne = new Model(renderer, "resources/suzanne.obj");
            plane = new Model(renderer, "resources/plane.obj");

            dragon.Transform.Scale = new Vector3(0.5f);
            dragon.Transform.Position = new Vector3(-0.1f, 0, -0.25f);

            suzanne.Transform.Scale = new Vector3(0.25f);
            suzanne.Transform.Position = new Vector3(0.2f, 0, 0);

            plane.Transform.Scale = new Vector3(2);
            plane.Transform.Position = new Vector3(0, -0.35f, -0.5f);

            var dragonColor = new Texture(renderer, TextureType.Image, "resources/dragon_texture_color.png", true);
            var dragonNormal = new Texture(renderer, TextureType.Image, "resources/dragon_texture_normal.png");
            var dragonEffects = new Texture(renderer, TextureType.Image, "resources/dragon_texture_ao_specular_reflection.png");

            var suzanneColor = new Texture(renderer, TextureType.Image, "resources/suzanne_texture_color.png", true);
            var suzanneNormal = new Texture(renderer, TextureType.Image, "resources/suzanne_texture_normal.png");
            var suzanneEffects = new Texture(renderer, TextureType.Image, "resources/suzanne_texture_ao_specular_reflection.png");

            var planeColor = new Texture(renderer, TextureType.Image, "resources/plane_texture_color.png", true);
            var planeNormal = new Texture(renderer, TextureType.Image, "resources/plane_texture_normal.png");
            var planeEffects = new Texture(renderer, TextureType.Image, "resources/plane_texture_depthmap.png");

            var skyColor = new Texture(renderer, TextureType.Cubemap, "resources/cubemap/cubemap", true);
            var skySmallColor = new Texture(renderer, TextureType.Cubemap, "resources/cubemap/cubemap_diff", true);

            dragonMat = new Material(renderer, sampler, new List<Texture> { dragonColor, dragonNormal, dragonEffects });
            suzanneMat = new Material(renderer, sampler, new List<Texture> { suzanneColor, suzanneNormal, suzanneEffects });
            planeMat = new Material(renderer, sampler, new List<Texture> { planeColor, planeNormal, planeEffects });

            textures = new DisposableList<Texture> {
                dragonColor, dragonNormal, dragonEffects,
                suzanneColor, suzanneNormal, suzanneEffects,
                planeColor, planeNormal, planeEffects,
                skyColor, skySmallColor
            };

            UploadResources(textures);
        }

        public void Dispose() {
            renderer.Device.WaitIdle();
            sampler.Dispose();
            uniformSetLayout.Dispose();
            textureSetLayout.Dispose();
            modelSetLayout.Dispose();
            camUniform.Dispose();
            lightUniform.Dispose();
            dragon.Dispose();
            suzanne.Dispose();
            plane.Dispose();
            dragonMat.Dispose();
            suzanneMat.Dispose();
            planeMat.Dispose();
            textures.Dispose();
            renderer.Dispose();
        }

        void UploadResources(IList<Texture> textures) {
            CommandBuffer commandBuffer = renderer.GetSingleUseCommandBuffer();

            using (var stagingBuffers = new DisposableList<StagingBuffer>()) {
                foreach (var texture in textures) {
                    texture.UploadData(commandBuffer, stagingBuffers);
                }

                dragon.UploadData(commandBuffer, stagingBuffers);
                suzanne.UploadData(commandBuffer, stagingBuffers);
                plane.UploadData(commandBuffer, stagingBuffers);

                renderer.SubmitCommandBuffer(commandBuffer);
            }
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
