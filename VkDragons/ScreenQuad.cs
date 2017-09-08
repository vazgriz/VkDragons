using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

namespace VkDragons {
    public class ScreenQuad : IDisposable {
        Renderer renderer;

        Buffer vertexBuffer;
        Buffer indexBuffer;
        Allocation vertexAlloc;
        Allocation indexAlloc;

        List<Vector3> positions = new List<Vector3> {
            new Vector3(-1, -1, 0),
            new Vector3( 1, -1, 0),
            new Vector3(-1,  1, 0),
            new Vector3( 1,  1, 0),
        };

        List<uint> indices = new List<uint> {
            0, 1, 2,
            1, 3, 2
        };

        public ScreenQuad(Renderer renderer) {
            this.renderer = renderer;

            CreateBuffer((ulong)Interop.SizeOf(positions), VkBufferUsageFlags.VertexBufferBit, out vertexBuffer, out vertexAlloc);
            CreateBuffer((ulong)Interop.SizeOf(indices), VkBufferUsageFlags.IndexBufferBit, out indexBuffer, out indexAlloc);
        }

        public void Dispose() {
            vertexBuffer.Dispose();
            indexBuffer.Dispose();
            renderer.Memory.Free(vertexAlloc);
            renderer.Memory.Free(indexAlloc);
        }

        public void Draw(CommandBuffer commandBuffer) {
            commandBuffer.BindVertexBuffers(0, vertexBuffer, 0);
            commandBuffer.BindIndexBuffer(indexBuffer, 0, VkIndexType.Uint32);
            commandBuffer.DrawIndexed((uint)indices.Count, 1, 0, 0, 0);
        }

        public void UploadData(CommandBuffer commandBuffer, DisposableList<StagingBuffer> stagingBuffers) {
            var vertexStaging = new StagingBuffer(renderer, vertexAlloc.size);
            vertexStaging.Fill(positions);
            vertexStaging.CopyToBuffer(commandBuffer, vertexBuffer);
            stagingBuffers.Add(vertexStaging);

            var indexStaging = new StagingBuffer(renderer, indexAlloc.size);
            indexStaging.Fill(indices);
            indexStaging.CopyToBuffer(commandBuffer, indexBuffer);
            stagingBuffers.Add(indexStaging);
        }

        void CreateBuffer(ulong size, VkBufferUsageFlags usage, out Buffer buffer, out Allocation alloc) {
            BufferCreateInfo info = new BufferCreateInfo {
                size = size,
                sharingMode = VkSharingMode.Exclusive,
                usage = VkBufferUsageFlags.TransferDstBit | usage
            };

            buffer = new Buffer(renderer.Device, info);

            Allocator allocator = renderer.Memory.GetDeviceAllocator(buffer.Requirements);
            alloc = allocator.Alloc(buffer.Requirements);

            buffer.Bind(alloc.memory, alloc.offset);
        }

        public static List<VkVertexInputBindingDescription> BindingDescriptions {
            get {
                return new List<VkVertexInputBindingDescription> {
                    new VkVertexInputBindingDescription {    //position
                        binding = 0,
                        stride = (uint)Interop.SizeOf<Vector3>(),
                        inputRate = VkVertexInputRate.Vertex
                    },
                };
            }
        }

        public static List<VkVertexInputAttributeDescription> AttributeDescriptions {
            get {
                return new List<VkVertexInputAttributeDescription> {
                    new VkVertexInputAttributeDescription {
                        binding = 0,
                        location = 0,
                        format = VkFormat.R32g32b32Sfloat,
                    },
                };
            }
        }
    }
}
