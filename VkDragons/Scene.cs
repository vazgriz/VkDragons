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

    public partial class Scene : IDisposable {
        Renderer renderer;
        Camera camera;
        Input input;
        Light light;

        List<CommandBuffer> commandBuffers;

        Sampler sampler;
        DescriptorSetLayout uniformSetLayout;
        DescriptorSetLayout textureSetLayout;
        DescriptorSetLayout modelSetLayout;

        UniformBuffer camUniform;
        UniformBuffer lightUniform;

        RenderPass lightRenderPass;
        RenderPass boxBlurRenderPass;
        RenderPass screenQuadRenderPass;
        RenderPass geometryRenderPass;
        RenderPass mainRenderPass;

        List<Framebuffer> mainFramebuffers;
        Framebuffer geometryFramebuffer;
        Framebuffer fxaaFramebuffer;

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
        Framebuffer lightFramebuffer;
        Framebuffer boxBlurFramebuffer;

        Material lightMat;

        Texture depth;
        Texture geometryTarget;
        Texture fxaaTarget;

        Material geometryMat;
        Material fxaaMat;

        DisposableList<Texture> textures;

        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public double Time { get; private set; }

        public Scene(Window window) {
            renderer = new Renderer(window);
            camera = new Camera(45, window.FramebufferWidth, window.FramebufferHeight);
            input = new Input(window, this, renderer, camera);
            light = new Light();
            commandBuffers = new List<CommandBuffer>();

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

            textures = new DisposableList<Texture> {
                dragonColor, dragonNormal, dragonEffects,
                suzanneColor, suzanneNormal, suzanneEffects,
                planeColor, planeNormal, planeEffects,
                skyColor, skySmallColor
            };

            UploadResources(textures);

            lightDepth = new Texture(renderer, TextureType.Depth, 512, 512, VkImageUsageFlags.DepthStencilAttachmentBit);
            lightColor = new Texture(renderer, TextureType.Image, lightDepth.Width, lightDepth.Height, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit, VkFormat.R8g8Unorm);
            boxBlur = new Texture(renderer, TextureType.Image, lightDepth.Width, lightDepth.Height, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit, lightColor.Format);

            dragonMat = new Material(renderer, sampler, new List<Texture> { dragonColor, dragonNormal, dragonEffects, skyColor, skySmallColor, boxBlur });
            suzanneMat = new Material(renderer, sampler, new List<Texture> { suzanneColor, suzanneNormal, suzanneEffects, skyColor, skySmallColor, boxBlur });
            planeMat = new Material(renderer, sampler, new List<Texture> { planeColor, planeNormal, planeEffects, skyColor, skySmallColor, boxBlur });
            skyMat = new Material(renderer, sampler, new List<Texture> { skyColor });

            textures.Add(lightDepth);
            textures.Add(lightColor);
            textures.Add(boxBlur);

            lightMat = new Material(renderer, sampler, new List<Texture> { lightColor });

            CreateScreenQuadRenderPass();
            CreateLightRenderPass();
            CreateBoxBlurRenderPass();

            lightFramebuffer = CreateFramebuffer(lightRenderPass, lightColor.Width, lightColor.Height, new List<ImageView> { lightColor.ImageView, lightDepth.ImageView });
            boxBlurFramebuffer = CreateFramebuffer(boxBlurRenderPass, boxBlur.Width, boxBlur.Height, new List<ImageView> { boxBlur.ImageView });

            CreateSwapchainResources(Width, Height);

            CreatePipelines();
            AllocateCommandBuffers();
        }

        public void Dispose() {
            renderer.Device.WaitIdle();
            CleanupSwapchainResources();
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
            lightFramebuffer.Dispose();
            boxBlurFramebuffer.Dispose();
            DestroyPipelines();
            renderer.Dispose();
        }

        void CleanupSwapchainResources() {
            depth.Dispose();
            geometryTarget.Dispose();
            fxaaTarget.Dispose();
            geometryMat.Dispose();
            fxaaMat.Dispose();
            geometryRenderPass.Dispose();
            mainRenderPass.Dispose();
            foreach (var fb in mainFramebuffers) fb.Dispose();
            geometryFramebuffer.Dispose();
            fxaaFramebuffer.Dispose();
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
                quad.UploadData(commandBuffer, stagingBuffers);
                skybox.UploadData(commandBuffer, stagingBuffers);

                renderer.SubmitCommandBuffer(commandBuffer);
            }
        }

        void UpdateUniform() {
            Matrix4x4 inverse;
            Matrix4x4.Invert(camera.View, out inverse);

            camUniform.Fill(new CameraUniform {
                camProjection = camera.Projection,
                camView = camera.View,
                camRotationOnlyView = camera.RotationOnlyView,
                camViewInverse = inverse
            });

            lightUniform.Fill(new LightUniform {
                lightPosition = light.Position,
                lightProjection = light.Projection,
                lightView = light.View,
                lightIa = light.Ia,
                lightId = light.Id,
                lightIs = light.Is,
                lightShininess = light.Shininess
            });
        }

        public void Update(double elapsed) {
            Time += elapsed;
            input.Update(elapsed);
            camera.Update();
            light.Position = new Vector4(2.0f, (1.5f + (float)Math.Sin(0.5f * Time)), 2.0f, 0);
            light.Update();
            UpdateUniform();
            suzanne.Transform.Rotation = Quaternion.CreateFromYawPitchRoll((float)Time, 0, 0);
        }

        public void Render() {
            renderer.Acquire();
            uint index = renderer.ImageIndex;
            RecordCommandBuffer(index);
            renderer.Submit(commandBuffers[(int)index]);
            renderer.Present();
        }

        public void Resize(int width, int height) {
            Width = (uint)width;
            Height = (uint)height;
            renderer.Resize(width, height);
            camera.SetSize(width, height);
            CleanupSwapchainResources();

            CreateSwapchainResources(Width, Height);
            AllocateCommandBuffers();
        }

        void CreateSwapchainResources(uint width, uint height) {
            depth = new Texture(renderer, TextureType.Depth, width, height, VkImageUsageFlags.None);
            geometryTarget = new Texture(renderer, TextureType.Image, width, height, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit, VkFormat.R8g8b8a8Unorm);
            fxaaTarget = new Texture(renderer, TextureType.Image, width, height, VkImageUsageFlags.ColorAttachmentBit | VkImageUsageFlags.SampledBit, VkFormat.R8g8b8a8Unorm);
            geometryMat = new Material(renderer, sampler, new List<Texture> { geometryTarget });
            fxaaMat = new Material(renderer, sampler, new List<Texture> { fxaaTarget });

            CreateGeometryRenderPass();
            CreateMainRenderPass();

            mainFramebuffers = new List<Framebuffer>(renderer.SwapchainImageViews.Count);

            for (int i = 0; i < renderer.SwapchainImageViews.Count; i++) {
                mainFramebuffers.Add(CreateFramebuffer(mainRenderPass, width, height, new List<ImageView> { renderer.SwapchainImageViews[i] }));
            }

            geometryFramebuffer = CreateFramebuffer(geometryRenderPass, width, height, new List<ImageView> { geometryTarget.ImageView, depth.ImageView });
            fxaaFramebuffer = CreateFramebuffer(screenQuadRenderPass, width, height, new List<ImageView> { fxaaTarget.ImageView });
        }

        void RecordCommandBuffer(uint imageIndex) {
            CommandBuffer commandBuffer = commandBuffers[(int)imageIndex];

            commandBuffer.Reset(VkCommandBufferResetFlags.None);

            commandBuffer.Begin(new CommandBufferBeginInfo {
                flags = VkCommandBufferUsageFlags.OneTimeSubmitBit
            });

            RecordDepthPass(commandBuffer);
            RecordBoxBlurPass(commandBuffer);
            RecordGeometryPass(commandBuffer);
            RecordFXAAPass(commandBuffer);
            RecordMainPass(commandBuffer, imageIndex);

            commandBuffer.End();
        }

        void RecordDepthPass(CommandBuffer commandBuffer) {
            VkClearValue colorClear = new VkClearValue();
            colorClear.color.float32_0 = 1;
            colorClear.color.float32_1 = 1;
            colorClear.color.float32_2 = 1;
            colorClear.color.float32_3 = 1;

            VkClearValue depthClear = new VkClearValue();
            depthClear.depthStencil.depth = 1;

            commandBuffer.BeginRenderPass(new RenderPassBeginInfo {
                renderPass = lightRenderPass,
                clearValues = new List<VkClearValue> {
                    colorClear, depthClear
                },
                framebuffer = lightFramebuffer,
                renderArea = new VkRect2D {
                    extent = new VkExtent2D {
                        width = lightDepth.Width,
                        height = lightDepth.Height
                    }
                }
            }, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, lightPipeline);

            commandBuffer.SetViewports(0, new VkViewport {
                width = lightDepth.Width,
                height = lightDepth.Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = new VkExtent2D {
                    width = lightDepth.Width,
                    height = lightDepth.Height
                }
            });

            camUniform.Bind(commandBuffer, lightPipelineLayout, 0);
            lightUniform.Bind(commandBuffer, lightPipelineLayout, 1);

            dragon.DrawDepth(commandBuffer, lightPipelineLayout, camera);
            suzanne.DrawDepth(commandBuffer, lightPipelineLayout, camera);

            commandBuffer.EndRenderPass();
        }

        void RecordBoxBlurPass(CommandBuffer commandBuffer) {
            commandBuffer.BeginRenderPass(new RenderPassBeginInfo {
                renderPass = boxBlurRenderPass,
                framebuffer = boxBlurFramebuffer,
                renderArea = new VkRect2D {
                    extent = new VkExtent2D {
                        width = boxBlur.Width,
                        height = boxBlur.Height
                    }
                }
            }, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, boxBlurPipeline);

            commandBuffer.SetViewports(0, new VkViewport {
                width = boxBlur.Width,
                height = boxBlur.Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = new VkExtent2D {
                    width = boxBlur.Width,
                    height = boxBlur.Height
                }
            });

            lightMat.Bind(commandBuffer, screenQuadPipelineLayout, 0);

            quad.Draw(commandBuffer);

            commandBuffer.EndRenderPass();
        }

        void RecordGeometryPass(CommandBuffer commandBuffer) {
            VkClearValue colorClear = new VkClearValue();

            VkClearValue depthClear = new VkClearValue();
            depthClear.depthStencil.depth = 1;

            commandBuffer.BeginRenderPass(new RenderPassBeginInfo {
                renderPass = geometryRenderPass,
                clearValues = new List<VkClearValue> {
                    colorClear, depthClear
                },
                framebuffer = geometryFramebuffer,
                renderArea = new VkRect2D {
                    extent = renderer.SwapchainExtent
                }
            }, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, modelPipeline);

            commandBuffer.SetViewports(0, new VkViewport {
                width = Width,
                height = Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = renderer.SwapchainExtent
            });

            camUniform.Bind(commandBuffer, modelPipelineLayout, 0);
            lightUniform.Bind(commandBuffer, modelPipelineLayout, 1);

            dragonMat.Bind(commandBuffer, modelPipelineLayout, 2);
            dragon.Draw(commandBuffer, modelPipelineLayout, camera);

            suzanneMat.Bind(commandBuffer, modelPipelineLayout, 2);
            suzanne.Draw(commandBuffer, modelPipelineLayout, camera);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, planePipeline);

            planeMat.Bind(commandBuffer, modelPipelineLayout, 2);
            plane.Draw(commandBuffer, modelPipelineLayout, camera);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, skyboxPipeline);

            commandBuffer.SetViewports(0, new VkViewport {
                width = Width,
                height = Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = renderer.SwapchainExtent
            });

            skyMat.Bind(commandBuffer, skyboxPipelineLayout, 1);
            skybox.Draw(commandBuffer);

            commandBuffer.EndRenderPass();
        }

        void RecordFXAAPass(CommandBuffer commandBuffer) {
            commandBuffer.BeginRenderPass(new RenderPassBeginInfo {
                renderPass = screenQuadRenderPass,
                framebuffer = fxaaFramebuffer,
                renderArea = new VkRect2D {
                    extent = renderer.SwapchainExtent
                }                
            }, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, fxaaPipeline);
            geometryMat.Bind(commandBuffer, screenQuadPipelineLayout, 0);

            commandBuffer.SetViewports(0, new VkViewport {
                width = Width,
                height = Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = renderer.SwapchainExtent
            });

            quad.Draw(commandBuffer);

            commandBuffer.EndRenderPass();
        }

        void RecordMainPass(CommandBuffer commandBuffer, uint imageIndex) {
            commandBuffer.BeginRenderPass(new RenderPassBeginInfo {
                renderPass = mainRenderPass,
                framebuffer = mainFramebuffers[(int)imageIndex],
                renderArea = new VkRect2D {
                    extent = renderer.SwapchainExtent
                }
            }, VkSubpassContents.Inline);

            commandBuffer.BindPipeline(VkPipelineBindPoint.Graphics, finalPipeline);
            geometryMat.Bind(commandBuffer, screenQuadPipelineLayout, 0);

            commandBuffer.SetViewports(0, new VkViewport {
                width = Width,
                height = Height,
                maxDepth = 1
            });
            commandBuffer.SetScissor(0, new VkRect2D {
                extent = renderer.SwapchainExtent
            });

            quad.Draw(commandBuffer);

            commandBuffer.EndRenderPass();
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

        void CreateGeometryRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                format = geometryTarget.Format,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.DontCare,
                storeOp = VkAttachmentStoreOp.Store,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.ShaderReadOnlyOptimal
            };

            AttachmentDescription depthAttachment = new AttachmentDescription {
                format = depth.Format,
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
                attachments = new List<AttachmentDescription> { colorAttachment, depthAttachment },
                subpasses = new List<SubpassDescription> { subpass },
                dependencies = new List<SubpassDependency> { fromExternal, toExternal }
            };

            geometryRenderPass = new RenderPass(renderer.Device, info);
        }

        void CreateMainRenderPass() {
            AttachmentDescription colorAttachment = new AttachmentDescription {
                format = renderer.SwapchainFormat,
                samples = VkSampleCountFlags._1Bit,
                loadOp = VkAttachmentLoadOp.DontCare,
                storeOp = VkAttachmentStoreOp.Store,
                initialLayout = VkImageLayout.Undefined,
                finalLayout = VkImageLayout.PresentSrcKhr
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
                dstStageMask = VkPipelineStageFlags.BottomOfPipeBit,
                dstAccessMask = VkAccessFlags.None
            };

            RenderPassCreateInfo info = new RenderPassCreateInfo {
                attachments = new List<AttachmentDescription> { colorAttachment },
                subpasses = new List<SubpassDescription> { subpass },
                dependencies = new List<SubpassDependency> { fromExternal, toExternal }
            };

            mainRenderPass = new RenderPass(renderer.Device, info);
        }

        void AllocateCommandBuffers() {
            if (commandBuffers.Count > 0) renderer.CommandPool.Free(commandBuffers);

            commandBuffers = new List<CommandBuffer>(renderer.CommandPool.Allocate(VkCommandBufferLevel.Primary, renderer.SwapchainImages.Count));
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

        Framebuffer CreateFramebuffer(RenderPass renderPass, uint width, uint height, List<ImageView> imageViews) {
            FramebufferCreateInfo info = new FramebufferCreateInfo {
                renderPass = renderPass,
                width = width,
                height = height,
                layers = 1,
                attachments = imageViews
            };

            return new Framebuffer(renderer.Device, info);
        }
    }
}
