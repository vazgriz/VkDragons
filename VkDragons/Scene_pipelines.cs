using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

using CSGL;
using CSGL.GLFW;
using CSGL.Vulkan;

namespace VkDragons {
    public partial class Scene {
        PipelineLayout modelPipelineLayout;
        PipelineLayout skyboxPipelineLayout;
        PipelineLayout lightPipelineLayout;
        PipelineLayout screenQuadPipelineLayout;

        GraphicsPipeline modelPipeline;
        GraphicsPipeline lightPipeline;
        GraphicsPipeline boxBlurPipeline;
        GraphicsPipeline skyboxPipeline;
        GraphicsPipeline fxaaPipeline;
        GraphicsPipeline finalPipeline;

        void CreatePipelines() {
            CreateModelPipelineLayout();
            CreateModelPipeline();
            CreateLightPipelineLayout();
            CreateLightPipeline();
            CreateSkyboxPipelineLayout();
            CreateSkyboxPipeline();
            CreateScreenQuadPipelineLayout();
            CreateBoxBlurPipeline();
            CreateFXAAPipeline();
            CreateFinalPipeline();
        }

        void DestroyPipelines() {
            modelPipelineLayout.Dispose();
            modelPipeline.Dispose();
            lightPipelineLayout.Dispose();
            lightPipeline.Dispose();
            skyboxPipelineLayout.Dispose();
            skyboxPipeline.Dispose();
            screenQuadPipelineLayout.Dispose();
            boxBlurPipeline.Dispose();
            fxaaPipeline.Dispose();
            finalPipeline.Dispose();
        }

        void RecreatePipelines() {

        }

        ShaderModule CreateShader(string filename) {
            byte[] data = File.ReadAllBytes(filename);

            ShaderModuleCreateInfo info = new ShaderModuleCreateInfo {
                data = data
            };

            return new ShaderModule(renderer.Device, info);
        }

        void CreateModelPipelineLayout() {
            PipelineLayoutCreateInfo info = new PipelineLayoutCreateInfo {
                setLayouts = new List<DescriptorSetLayout> {
                    uniformSetLayout, uniformSetLayout, modelSetLayout
                },
                pushConstantRanges = new List<VkPushConstantRange> {
                    new VkPushConstantRange {
                        offset = 0,
                        size = (uint)(Interop.SizeOf<Matrix4x4>() + 3 * Interop.SizeOf<Vector4>()),
                        stageFlags = VkShaderStageFlags.VertexBit
                    }
                }
            };

            modelPipelineLayout = new PipelineLayout(renderer.Device, info);
        }

        void CreateModelPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/object.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/object.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main"
            };

            var bindings = Model.BindingDescriptions;
            var attributes = Model.AttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.BackBit,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo {
                depthTestEnable = true,
                depthWriteEnable = true,
                depthCompareOp = VkCompareOp.Less,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                depthStencilState = depthStencil,
                dynamicState = dynamicState,
                layout = modelPipelineLayout,
                renderPass = geometryRenderPass,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                modelPipeline = new GraphicsPipeline(renderer.Device, info, null);
            }
        }

        void CreateSkyboxPipelineLayout() {
            PipelineLayoutCreateInfo info = new PipelineLayoutCreateInfo {
                setLayouts = new List<DescriptorSetLayout> {
                    uniformSetLayout, textureSetLayout
                }
            };

            skyboxPipelineLayout = new PipelineLayout(renderer.Device, info);
        }

        void CreateSkyboxPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/cube.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/cube.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main"
            };

            var bindings = Skybox.BindingDescriptions;
            var attributes = Skybox.AttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo {
                depthTestEnable = true,
                depthWriteEnable = false,
                depthCompareOp = VkCompareOp.LessOrEqual,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                depthStencilState = depthStencil,
                dynamicState = dynamicState,
                layout = skyboxPipelineLayout,
                renderPass = geometryRenderPass,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                skyboxPipeline = new GraphicsPipeline(renderer.Device, info, null);
            }
        }

        void CreateLightPipelineLayout() {
            PipelineLayoutCreateInfo info = new PipelineLayoutCreateInfo {
                setLayouts = new List<DescriptorSetLayout> {
                    uniformSetLayout, uniformSetLayout
                },
                pushConstantRanges = new List<VkPushConstantRange> {
                    new VkPushConstantRange {
                        offset = 0,
                        size = (uint)(Interop.SizeOf<Matrix4x4>()),
                        stageFlags = VkShaderStageFlags.VertexBit
                    }
                }
            };

            lightPipelineLayout = new PipelineLayout(renderer.Device, info);
        }

        void CreateLightPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/object_depth.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/object_depth.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main"
            };

            var bindings = Model.DepthBindingDescriptions;
            var attributes = Model.DepthAttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.BackBit,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var depthStencil = new PipelineDepthStencilStateCreateInfo {
                depthTestEnable = true,
                depthWriteEnable = true,
                depthCompareOp = VkCompareOp.Less,
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                depthStencilState = depthStencil,
                dynamicState = dynamicState,
                layout = lightPipelineLayout,
                renderPass = lightRenderPass,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                lightPipeline = new GraphicsPipeline(renderer.Device, info, null);
            }
        }

        void CreateScreenQuadPipelineLayout() {
            PipelineLayoutCreateInfo info = new PipelineLayoutCreateInfo {
                setLayouts = new List<DescriptorSetLayout> {
                    textureSetLayout
                }
            };

            screenQuadPipelineLayout = new PipelineLayout(renderer.Device, info);
        }

        void CreateBoxBlurPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/boxblur.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/boxblur.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main",
            };

            var bindings = ScreenQuad.BindingDescriptions;
            var attributes = ScreenQuad.AttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                dynamicState = dynamicState,
                layout = screenQuadPipelineLayout,
                renderPass = boxBlurRenderPass,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                boxBlurPipeline = new GraphicsPipeline(renderer.Device, info, null);
            }
        }

        void CreateFXAAPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/screenquad.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/fxaa.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            byte[] specializationData = new byte[8];
            Interop.Copy(1f / renderer.SwapchainExtent.width, specializationData, 0);
            Interop.Copy(1f / renderer.SwapchainExtent.height, specializationData, 4);

            List<VkSpecializationMapEntry> entries = new List<VkSpecializationMapEntry> {
                new VkSpecializationMapEntry {
                    constantID = 0,
                    offset = 0,
                    size = (IntPtr)Interop.SizeOf<float>()
                },
                new VkSpecializationMapEntry {
                    constantID = 1,
                    offset = (uint)Interop.SizeOf<float>(),
                    size = (IntPtr)Interop.SizeOf<float>()
                }
            };

            SpecializationInfo specializationInfo = new SpecializationInfo {
                data = specializationData,
                mapEntries = entries
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main",
                specializationInfo = specializationInfo
            };

            var bindings = ScreenQuad.BindingDescriptions;
            var attributes = ScreenQuad.AttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            GraphicsPipeline old = fxaaPipeline;

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                dynamicState = dynamicState,
                layout = screenQuadPipelineLayout,
                renderPass = screenQuadRenderPass,
                basePipelineHandle = old,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                fxaaPipeline = new GraphicsPipeline(renderer.Device, info, null);
                if (old != null) old.Dispose();
            }
        }

        void CreateFinalPipeline() {
            ShaderModule vert = CreateShader("resources/shaders/screenquad.vert.spv");
            ShaderModule frag = CreateShader("resources/shaders/final_screenquad.frag.spv");

            var vertInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.VertexBit,
                module = vert,
                name = "main"
            };

            byte[] specializationData = new byte[8];
            Interop.Copy(renderer.Gamma ? 0 : 1, specializationData, 0);
            Interop.Copy(2.2f, specializationData, 4);

            List<VkSpecializationMapEntry> entries = new List<VkSpecializationMapEntry> {
                new VkSpecializationMapEntry {
                    constantID = 0,
                    offset = 0,
                    size = (IntPtr)Interop.SizeOf<uint>()
                },
                new VkSpecializationMapEntry {
                    constantID = 1,
                    offset = (uint)Interop.SizeOf<uint>(),
                    size = (IntPtr)Interop.SizeOf<float>()
                }
            };

            SpecializationInfo specializationInfo = new SpecializationInfo {
                data = specializationData,
                mapEntries = entries
            };

            var fragInfo = new PipelineShaderStageCreateInfo {
                stage = VkShaderStageFlags.FragmentBit,
                module = frag,
                name = "main",
                specializationInfo = specializationInfo
            };

            var bindings = ScreenQuad.BindingDescriptions;
            var attributes = ScreenQuad.AttributeDescriptions;

            var vertexInputInfo = new PipelineVertexInputStateCreateInfo {
                vertexBindingDescriptions = bindings,
                vertexAttributeDescriptions = attributes
            };

            var inputAssembly = new PipelineInputAssemblyStateCreateInfo {
                topology = VkPrimitiveTopology.TriangleList,
            };

            var viewportState = new PipelineViewportStateCreateInfo {
                viewports = new List<VkViewport> { default(VkViewport) },
                scissors = new List<VkRect2D> { default(VkRect2D) }
            };

            var rasterizer = new PipelineRasterizationStateCreateInfo {
                polygonMode = VkPolygonMode.Fill,
                lineWidth = 1,
                cullMode = VkCullModeFlags.None,
                frontFace = VkFrontFace.CounterClockwise
            };

            var multisampling = new PipelineMultisampleStateCreateInfo {
                rasterizationSamples = VkSampleCountFlags._1Bit
            };

            var colorBlending = new PipelineColorBlendStateCreateInfo {
                attachments = new List<PipelineColorBlendAttachmentState> {
                    new PipelineColorBlendAttachmentState {
                        colorWriteMask = VkColorComponentFlags.RBit | VkColorComponentFlags.GBit | VkColorComponentFlags.BBit | VkColorComponentFlags.ABit
                    }
                }
            };

            var dynamicState = new PipelineDynamicStateCreateInfo {
                dynamicStates = new List<VkDynamicState> {
                    VkDynamicState.Viewport,
                    VkDynamicState.Scissor
                }
            };

            GraphicsPipeline old = finalPipeline;

            var info = new GraphicsPipelineCreateInfo {
                stages = new List<PipelineShaderStageCreateInfo> { vertInfo, fragInfo },
                vertexInputState = vertexInputInfo,
                inputAssemblyState = inputAssembly,
                viewportState = viewportState,
                rasterizationState = rasterizer,
                multisampleState = multisampling,
                colorBlendState = colorBlending,
                dynamicState = dynamicState,
                layout = screenQuadPipelineLayout,
                renderPass = mainRenderPass,
                basePipelineHandle = old,
                subpass = 0,
            };

            using (vert)
            using (frag) {
                finalPipeline = new GraphicsPipeline(renderer.Device, info, null);
                if (old != null) old.Dispose();
            }
        }
    }
}
