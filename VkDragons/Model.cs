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

        Buffer[] buffers;
        Allocation[] allocations;

        public Model(Renderer renderer, string fileName) {
            this.renderer = renderer;

            buffers = new Buffer[6];
            allocations = new Allocation[6];
            mesh = new Mesh(fileName);
            Transform = new Transform();
            IndexCount = (uint)mesh.Indices.Count;
            CreateBuffers();
        }

        public void Dispose() {
            foreach (var buffer in buffers) {
                buffer.Dispose();
            }
        }

        public void Draw(CommandBuffer commandBuffer, PipelineLayout pipelineLayout, Camera camera) {
            List<Buffer> vertexBuffers = new List<Buffer> {
                buffers[0],
                buffers[1],
                buffers[2],
                buffers[3],
                buffers[4],
            };
            List<ulong> offsets = new List<ulong> {
                0, 0, 0, 0, 0
            };

            commandBuffer.BindVertexBuffers(0, vertexBuffers, offsets);
            commandBuffer.BindIndexBuffer(buffers[5], 0, VkIndexType.Uint32);

            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.VertexBit, 0, Transform.WorldMatrix);

            Matrix4x4 MV = camera.View * Transform.WorldMatrix;
            Matrix4x4 inverse;
            Matrix4x4.Invert(MV, out inverse);
            Matrix4x4 normal = Matrix4x4.Transpose(inverse);
            
            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.VertexBit,
                (uint)Interop.SizeOf<Matrix4x4>(), 3 * (uint)Interop.SizeOf<Vector4>(),
                Interop.AddressOf(ref inverse));

            commandBuffer.DrawIndexed(IndexCount, 1, 0, 0, 0);
        }

        public void DrawDepth(CommandBuffer commandBuffer, PipelineLayout pipelineLayout, Camera camera) {
            List<Buffer> vertexBuffers = new List<Buffer> {
                buffers[0],
            };
            List<ulong> offsets = new List<ulong> {
                0
            };

            commandBuffer.BindVertexBuffers(0, vertexBuffers, offsets);
            commandBuffer.BindIndexBuffer(buffers[5], 0, VkIndexType.Uint32);

            commandBuffer.PushConstants(pipelineLayout, VkShaderStageFlags.VertexBit, 0, Transform.WorldMatrix);

            commandBuffer.DrawIndexed(IndexCount, 1, 0, 0, 0);
        }

        void CreateBuffer(int index, ulong size, VkBufferUsageFlags usage) {
            BufferCreateInfo info = new BufferCreateInfo();
            info.size = size;
            info.usage = usage;
            info.sharingMode = VkSharingMode.Exclusive;

            Buffer buffer = new Buffer(renderer.Device, info);

            var allocator = renderer.Memory.GetDeviceAllocator(buffer.Requirements);
            var alloc = allocator.Alloc(buffer.Requirements);

            buffer.Bind(alloc.memory, alloc.offset);

            buffers[index] = buffer;
            allocations[index] = alloc;
        }

        void CreateBuffers() {
            CreateBuffer(0, (ulong)mesh.Positions.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            CreateBuffer(1, (ulong)mesh.Normals.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            CreateBuffer(2, (ulong)mesh.Tangents.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            CreateBuffer(3, (ulong)mesh.Binormals.Count * (ulong)Interop.SizeOf<Vector3>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            CreateBuffer(4, (ulong)mesh.TexCoords.Count * (ulong)Interop.SizeOf<Vector2>(),
                VkBufferUsageFlags.VertexBufferBit | VkBufferUsageFlags.TransferDstBit);
            CreateBuffer(5, IndexCount * (ulong)Interop.SizeOf<uint>(),
                VkBufferUsageFlags.IndexBufferBit | VkBufferUsageFlags.TransferDstBit);
        }

        public void UploadData(CommandBuffer commandBuffer, DisposableList<StagingBuffer> stagingBuffers) {
            int start = stagingBuffers.Count;
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.Positions)));
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.Normals)));
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.Tangents)));
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.Binormals)));
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.TexCoords)));
            stagingBuffers.Add(new StagingBuffer(renderer, (ulong)Interop.SizeOf(mesh.Indices)));

            stagingBuffers[start].Fill(mesh.Positions);
            stagingBuffers[start + 1].Fill(mesh.Normals);
            stagingBuffers[start + 2].Fill(mesh.Tangents);
            stagingBuffers[start + 3].Fill(mesh.Binormals);
            stagingBuffers[start + 4].Fill(mesh.TexCoords);
            stagingBuffers[start + 5].Fill(mesh.Indices);

            for (int i = 0; i < 6; i++) {
                stagingBuffers[start + i].CopyToBuffer(commandBuffer, buffers[i]);
            }

            mesh = null;
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
