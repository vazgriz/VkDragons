using System;
using System.Numerics;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

namespace VkDragons {
    public class Model : IDisposable {
        Renderer renderer;

        Mesh mesh;
        public Transform Transform { get; private set; }
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
            Transform = new Transform();
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

        Buffer CreateHostBuffer(ulong size, VkBufferUsageFlags usage) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.size = size;
            info.usage = usage;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer buffer = new Buffer(renderer.Device, info);

            var allocator = renderer.Memory.HostAllocator;
            var alloc = allocator.Alloc(buffer.Requirements.size, buffer.Requirements.alignment);

            buffer.Bind(alloc.memory, alloc.offset);

            return buffer;
        }

        Buffer Copy<T>(CommandBuffer commandBuffer, Buffer dest, List<T> source) where T : struct {
            ulong size = (ulong)source.Count * (ulong)Interop.SizeOf<T>();
            Buffer buffer = CreateHostBuffer(size, VkBufferUsageFlags.TransferSrcBit);
            IntPtr mapping = (IntPtr)((ulong)renderer.Memory.GetMapping(buffer.Memory) + buffer.Offset);
            Interop.Copy(source, mapping);

            VkBufferCopy copy = new VkBufferCopy();
            copy.size = size;
            copy.dstOffset = 0;
            copy.srcOffset = 0;

            commandBuffer.CopyBuffer(buffer, dest, copy);

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

        public void UploadData(CommandBuffer commandBuffer) {
            positionsStagingBuffer = Copy(commandBuffer, positionsBuffer, mesh.Positions);
            normalsStagingBuffer = Copy(commandBuffer, normalsBuffer, mesh.Normals);
            tangentsStagingBuffer = Copy(commandBuffer, tangentsBuffer, mesh.Tangents);
            binormalsStagingBuffer = Copy(commandBuffer, binormalsBuffer, mesh.Binormals);
            texCoordsStagingBuffer = Copy(commandBuffer, texCoordsBuffer, mesh.TexCoords);
            indexStagingBuffer = Copy(commandBuffer, indexBuffer, mesh.Indices);
        }

        public static List<VkVertexInputBindingDescription> BindingDescriptions {
            get {
                return new List<VkVertexInputBindingDescription> {
                    new VkVertexInputBindingDescription {    //position
                        binding = 0,
                        stride = (uint)Interop.SizeOf<Vector3>(),
                        inputRate = VkVertexInputRate.Vertex
                    },
                    new VkVertexInputBindingDescription {    //normal
                        binding = 1,
                        stride = (uint)Interop.SizeOf<Vector3>(),
                        inputRate = VkVertexInputRate.Vertex
                    },
                    new VkVertexInputBindingDescription {    //tangent
                        binding = 2,
                        stride = (uint)Interop.SizeOf<Vector3>(),
                        inputRate = VkVertexInputRate.Vertex
                    },
                    new VkVertexInputBindingDescription {    //binormal
                        binding = 3,
                        stride = (uint)Interop.SizeOf<Vector3>(),
                        inputRate = VkVertexInputRate.Vertex
                    },
                    new VkVertexInputBindingDescription {    //texcoord
                        binding = 4,
                        stride = (uint)Interop.SizeOf<Vector2>(),
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
                    new VkVertexInputAttributeDescription {
                        binding = 1,
                        location = 1,
                        format = VkFormat.R32g32b32Sfloat,
                    },
                    new VkVertexInputAttributeDescription {
                        binding = 2,
                        location = 2,
                        format = VkFormat.R32g32b32Sfloat,
                    },
                    new VkVertexInputAttributeDescription {
                        binding = 3,
                        location = 3,
                        format = VkFormat.R32g32b32Sfloat,
                    },
                    new VkVertexInputAttributeDescription {
                        binding = 4,
                        location = 4,
                        format = VkFormat.R32g32Sfloat,
                    },
                };
            }
        }
    }
}
