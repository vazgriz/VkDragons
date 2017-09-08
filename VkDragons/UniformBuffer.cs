using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;
using Image = CSGL.Vulkan.Image;

namespace VkDragons {
    public class UniformBuffer : IDisposable {
        Renderer renderer;
        ulong size;
        Buffer buffer;
        Allocation alloc;
        IntPtr mapping;

        DescriptorSetLayout layout;
        DescriptorPool pool;
        DescriptorSet set;

        public UniformBuffer(Renderer renderer, ulong size, DescriptorSetLayout layout) {
            this.renderer = renderer;
            this.size = size;
            this.layout = layout;

            CreateHostBuffer(size, VkBufferUsageFlags.UniformBufferBit);
            mapping = (IntPtr)((ulong)renderer.Memory.GetMapping(alloc.memory) + alloc.offset);

            CreatePool();
            CreateSet();
        }

        public void Dispose() {
            renderer.Memory.HostAllocator.Free(alloc);
            buffer.Dispose();
            pool.Dispose();
        }

        public void Fill<T>(T data) where T : struct {
            Interop.Copy(data, mapping);
        }

        public void Bind(CommandBuffer commandBuffer, PipelineLayout pipelineLayout, uint firstSet) {
            commandBuffer.BindDescriptorSets(VkPipelineBindPoint.Graphics, pipelineLayout, firstSet, set, null);
        }

        void CreateHostBuffer(ulong size, VkBufferUsageFlags usage) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.size = size;
            info.usage = usage;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer buffer = new Buffer(renderer.Device, info);

            var allocator = renderer.Memory.HostAllocator;
            var alloc = allocator.Alloc(buffer.Requirements);

            buffer.Bind(alloc.memory, alloc.offset);

            this.buffer = buffer;
            this.alloc = alloc;
        }

        void CreatePool() {
            VkDescriptorPoolSize size = new VkDescriptorPoolSize {
                type = VkDescriptorType.UniformBuffer,
                descriptorCount = 1
            };

            DescriptorPoolCreateInfo info = new DescriptorPoolCreateInfo {
                maxSets = 1,
                poolSizes = new List<VkDescriptorPoolSize> { size }
            };

            pool = new DescriptorPool(renderer.Device, info);
        }

        void CreateSet() {
            DescriptorSetAllocateInfo info = new DescriptorSetAllocateInfo {
                setLayouts = new List<DescriptorSetLayout> { layout },
                descriptorSetCount = 1
            };

            set = pool.Allocate(info)[0];

            DescriptorBufferInfo bufferInfo = new DescriptorBufferInfo {
                buffer = buffer,
                offset = 0,
                range = size
            };

            WriteDescriptorSet write = new WriteDescriptorSet {
                dstSet = set,
                bufferInfo = new List<DescriptorBufferInfo> { bufferInfo },
                dstBinding = 0,
                dstArrayElement = 0,
                descriptorType = VkDescriptorType.UniformBuffer
            };

            set.Update(new List<WriteDescriptorSet> { write });
        }
    }
}
