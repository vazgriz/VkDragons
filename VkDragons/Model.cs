using System;
using System.Numerics;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

namespace VkDragons {
    public class Model : IDisposable {
        Renderer renderer;

        Mesh mesh;
        public uint IndexCount { get; private set; }

        Buffer positionsBuffer;
        Buffer normalsBuffer;
        Buffer tangentsBuffer;
        Buffer binormalsBuffer;
        Buffer texCoordsBuffer;
        Buffer indexBuffer;

        Buffer positionsStagingBuffer;
        Buffer normalsStagingBuffer;
        Buffer tangentsStagingBuffer;
        Buffer binormalsStagingBuffer;
        Buffer texCoordsStagingBuffer;
        Buffer indexStagingBuffer;

        public Model(Renderer renderer, string fileName) {
            this.renderer = renderer;

            mesh = new Mesh(fileName);
            IndexCount = (uint)mesh.Indices.Count;
            CreateBuffers();
        }

        public void Dispose() {
            positionsBuffer.Dispose();
            normalsBuffer.Dispose();
            tangentsBuffer.Dispose();
            binormalsBuffer.Dispose();
            texCoordsBuffer.Dispose();
            indexBuffer.Dispose();
        }

        public void DestroyStaging() {
            positionsStagingBuffer.Dispose();
            normalsStagingBuffer.Dispose();
            tangentsStagingBuffer.Dispose();
            binormalsStagingBuffer.Dispose();
            texCoordsStagingBuffer.Dispose();
            indexStagingBuffer.Dispose();
            mesh = null;
        }

        Buffer CreateBuffer(ulong size, VkBufferUsageFlags usage) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.size = size;
            info.usage = usage;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer buffer = new Buffer(renderer.Device, info);

            var allocator = renderer.Memory.GetDeviceAllocator(buffer.Requirements);
            var alloc = allocator.Alloc(buffer.Requirements.size, buffer.Requirements.alignment);

            buffer.Bind(alloc.memory, alloc.offset);

            return buffer;
        }

        void CreateBuffers() {
            positionsBuffer = CreateBuffer((ulong)mesh.Positions.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            normalsBuffer = CreateBuffer((ulong)mesh.Normals.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            tangentsBuffer = CreateBuffer((ulong)mesh.Tangents.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            binormalsBuffer = CreateBuffer((ulong)mesh.Binormals.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            texCoordsBuffer = CreateBuffer((ulong)mesh.TexCoords.Count * (ulong)Interop.SizeOf<Vector2>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            indexBuffer = CreateBuffer(IndexCount * (ulong)Interop.SizeOf<uint>(),
                VkBufferUsageFlags.IndexBufferBit | VkBufferUsageFlags.TransferDstBit);
        }
    }
}
