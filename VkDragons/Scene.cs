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

        RenderPass lightRenderPass;
        RenderPass boxBlurRenderPass;
        RenderPass screenQuadRenderPass;

        Model dragon;
        Model suzanne;
        Model plane;
        Skybox skybox;
        ScreenQuad quad;

        Material dragonMat;
        Material suzanneMat;
        Material planeMat;
        Material skyMat;

        Texture lightDepth;
        Texture lightColor;
        Texture boxBlur;

        Material lightMat;

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
            skybox = new Skybox(renderer);
            quad = new ScreenQuad(renderer);

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
            skyMat = new Material(renderer, sampler, new List<Texture> { skyColor });

            textures = new DisposableList<Texture> {
                dragonColor, dragonNormal, dragonEffects,
                suzanneColor, suzanneNormal, suzanneEffects,
                planeColor, planeNormal, planeEffects,
                skyColor, skySmallColor
            };

            UploadResources(textures);

            lightDepth = new Texture(renderer, TextureType.Depth, 512, 512, VkImageUsageFlags.SampledBit);
            lightColor = new Texture(renderer, TextureType.Image, lightDepth.Width, lightDepth.Height, VkImageUsageFlags.SampledBit, VkFormat.R8g8Unorm);
            boxBlur = new Texture(renderer, TextureType.Image, lightDepth.Width, lightDepth.Height, VkImageUsageFlags.SampledBit, lightColor.Format);

            textures.Add(lightDepth);
            textures.Add(lightColor);
            textures.Add(boxBlur);

            lightMat = new Material(renderer, sampler, new List<Texture> { lightColor });

            CreateScreenQuadRenderPass();
            CreateLightRenderPass();
            CreateBoxBlurRenderPass();
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
            skybox.Dispose();
            quad.Dispose();
            dragonMat.Dispose();
            suzanneMat.Dispose();
            planeMat.Dispose();
            skyMat.Dispose();
            lightMat.Dispose();
            textures.Dispose();
            screenQuadRenderPass.Dispose();
            lightRenderPass.Dispose();
            boxBlurRenderPass.Dispose();
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

        void CreateScreenQuadRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                format = VkFormat.R8g8b8a8Unorm,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.DontCare,
                storeOp = VkAttachmentStoreOp.Store,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentReference colorRef = new AttachmentReference {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            SubpassDescription subpass = new SubpassDescription {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachments = new List<AttachmentReference> { colorRef }
            };

            SubpassDependency fromExternal = new SubpassDependency {
                srcSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.ShaderReadBit,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
            };

            SubpassDependency toExternal = new SubpassDependency {
                srcSubpass = 0,
                dstSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                dstStageMask = VkPipelineStageFlags.FragmentShaderBit | VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ShaderReadBit | VkAccessFlags.ColorAttachmentWriteBit
            };

            RenderPassCreateInfo info = new RenderPassCreateInfo {
                attachments = new List<AttachmentDescription> { colorAttachment },
                subpasses = new List<SubpassDescription> { subpass },
                dependencies = new List<SubpassDependency> { fromExternal, toExternal }
            };

            screenQuadRenderPass = new RenderPass(renderer.Device, info);
        }

        void CreateLightRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                format = lightColor.Format,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.Store,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };
            AttachmentDescription depthAttachment = new AttachmentDescription {
                format = lightDepth.Format,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.Clear,
                storeOp = VkAttachmentStoreOp.DontCare,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            AttachmentReference colorRef = new AttachmentReference {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            AttachmentReference depthRef = new AttachmentReference {
                attachment = 1,
                layout = VkImageLayout.DepthStencilAttachmentOptimal
            };

            SubpassDescription subpass = new SubpassDescription {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachments = new List<AttachmentReference> { colorRef },
                depthStencilAttachment = depthRef                
            };

            SubpassDependency fromExternal = new SubpassDependency {
                srcSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.None,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
            };

            SubpassDependency toExternal = new SubpassDependency {
                srcSubpass = 0,
                dstSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                dstStageMask = VkPipelineStageFlags.FragmentShaderBit | VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ShaderReadBit | VkAccessFlags.ColorAttachmentWriteBit
            };

            RenderPassCreateInfo info = new RenderPassCreateInfo {
                attachments = new List<AttachmentDescription> { colorAttachment, depthAttachment },
                subpasses = new List<SubpassDescription> { subpass },
                dependencies = new List<SubpassDependency> { fromExternal, toExternal }
            };

            lightRenderPass = new RenderPass(renderer.Device, info);
        }

        void CreateBoxBlurRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                format = boxBlur.Format,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.DontCare,
                storeOp = VkAttachmentStoreOp.Store,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentReference colorRef = new AttachmentReference {
                attachment = 0,
                layout = VkImageLayout.ColorAttachmentOptimal
            };

            SubpassDescription subpass = new SubpassDescription {
                pipelineBindPoint = VkPipelineBindPoint.Graphics,
                colorAttachments = new List<AttachmentReference> { colorRef }
            };

            SubpassDependency fromExternal = new SubpassDependency {
                srcSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                dstSubpass = 0,
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.ColorAttachmentWriteBit | VkAccessFlags.ShaderReadBit,
                dstStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ColorAttachmentWriteBit
            };

            SubpassDependency toExternal = new SubpassDependency {
                srcSubpass = 0,
                dstSubpass = uint.MaxValue, //VK_SUBPASS_EXTERNAL
                srcStageMask = VkPipelineStageFlags.ColorAttachmentOutputBit,
                srcAccessMask = VkAccessFlags.ColorAttachmentWriteBit,
                dstStageMask = VkPipelineStageFlags.FragmentShaderBit | VkPipelineStageFlags.ColorAttachmentOutputBit,
                dstAccessMask = VkAccessFlags.ShaderReadBit | VkAccessFlags.ColorAttachmentWriteBit
            };

            RenderPassCreateInfo info = new RenderPassCreateInfo {
                attachments = new List<AttachmentDescription> { colorAttachment },
                subpasses = new List<SubpassDescription> { subpass },
                dependencies = new List<SubpassDependency> { fromExternal, toExternal }
            };

            boxBlurRenderPass = new RenderPass(renderer.Device, info);
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
