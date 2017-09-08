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

        void CreatePipelines() {
            CreateModelPipelineLayout();
            CreateModelPipeline();
        }

        void DestroyPipelines() {
            modelPipelineLayout.Dispose();
            modelPipeline.Dispose();
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
    }
}
