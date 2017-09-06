using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;

namespace VkDragons {
    public class Material {
        Renderer renderer;
        List<Texture> textures;
        Sampler sampler;
        DescriptorSetLayout layout;
        DescriptorPool pool;
        DescriptorSet set;

        public Material(Renderer renderer, Sampler sampler, List<Texture> textures) {
            this.renderer = renderer;
            this.sampler = sampler;
            this.textures = textures;

            CreateLayout();
            CreatePool();
            CreateSet();
            WriteDescriptors();
        }

        public void Dispose() {
            layout.Dispose();
            pool.Dispose();
        }

        public void Bind(CommandBuffer commandBuffer, PipelineLayout pipelineLayout, uint firstSet) {
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, firstSet, set, null);
        }

        void CreateLayout() {
            List<VkDescriptorSetLayoutBinding> bindings = new List<VkDescriptorSetLayoutBinding>(textures.Count);

            for (int i = 0; i < textures.Count; i++) {
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

            layout = new DescriptorSetLayout(renderer.Device, info);
        }

        void CreatePool() {
            VkDescriptorPoolSize size = new VkDescriptorPoolSize {
                type = VkDescriptorType.CombinedImageSampler,
                descriptorCount = (uint)textures.Count
            };

            DescriptorPoolCreateInfo info = new DescriptorPoolCreateInfo {
                maxSets = 1,
                poolSizes = new List<VkDescriptorPoolSize> { size }
            };

            pool = new DescriptorPool(renderer.Device, info);
        }

        void CreateSet() {
            DescriptorSetAllocateInfo info = new DescriptorSetAllocateInfo {
                descriptorSetCount = 1,
                setLayouts = new List<DescriptorSetLayout> { layout }
            };

            set = pool.Allocate(info)[0];
        }

        void WriteDescriptors() {
            List<WriteDescriptorSet> writes = new List<WriteDescriptorSet>(textures.Count);

            for (int i = 0; i < textures.Count; i++) {
                List<DescriptorImageInfo> imageInfos = new List<DescriptorImageInfo>{
                        new DescriptorImageInfo {
                        imageLayout = VkImageLayout.ShaderReadOnlyOptimal,
                        imageView = textures[i].ImageView,
                        sampler = sampler
                    }
                };

                writes.Add(new WriteDescriptorSet {
                    dstSet = set,
                    dstArrayElement = 0,
                    descriptorType = VkDescriptorType.CombinedImageSampler,
                    dstBinding = (uint)i,
                    imageInfo = imageInfos
                });

                set.Update(writes);
            }
        }
    }
}
