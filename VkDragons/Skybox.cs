using System;
using System.Collections.Generic;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

namespace VkDragons {
    public class Skybox : IDisposable {
        Renderer renderer;

        Buffer vertexBuffer;
        Buffer indexBuffer;
        Allocation vertexAlloc;
        Allocation indexAlloc;

        List<Vector3> positions = new List<Vector3> {
            new Vector3(-1, -1,  1),
            new Vector3( 1, -1,  1),
            new Vector3(-1,  1,  1),
            new Vector3( 1,  1,  1),
            new Vector3(-1, -1, -1),
            new Vector3( 1, -1, -1),
            new Vector3(-1,  1, -1),
            new Vector3( 1,  1, -1),
        };

        List<uint> indices = new List<uint> {
            2, 1, 0, 3, 1, 2,   // Back face
	        3, 5, 1, 7, 5, 3,   // Right face
	        7, 4, 5, 6, 4, 7,   // Front face
	        6, 0, 4, 2, 0, 6,   // Left face
	        1, 4, 0, 5, 4, 1,   // Bottom face
	        6, 3, 2, 7, 3, 6    // Top face
        };

        public Skybox(Renderer renderer) {
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
            commandBuffer.Draw((uint)indices.Count, 1, 0, 0);
        }

        public void UploadData(CommandBuffer commandBuffer, DisposableList<StagingBuffer> stagingBuffers) {
            var vertexStaging = new StagingBuffer(renderer, vertexAlloc.size);
            vertexStaging.Fill(positions);
            stagingBuffers.Add(vertexStaging);

            var indexStaging = new StagingBuffer(renderer, indexAlloc.size);
            indexStaging.Fill(indices);
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
    }
}
