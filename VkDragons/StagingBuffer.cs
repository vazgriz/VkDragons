﻿using System;
using System.Collections.Generic;

using CSGL;
using CSGL.Vulkan;
using Buffer = CSGL.Vulkan.Buffer;

namespace VkDragons {
    public class StagingBuffer<T> : IDisposable where T : struct {
        Renderer renderer;
        ulong size;
        Buffer buffer;

        public StagingBuffer(Renderer renderer, ulong size, List<T> data) {
            this.renderer = renderer;
            this.size = size;

            buffer = CreateHostBuffer((ulong)Interop.SizeOf(data), VkBufferUsageFlags.None);
            IntPtr mapping = renderer.Memory.GetMapping(buffer.Memory);

            Interop.Copy(data, mapping);
        }

        public void Dispose() {
            buffer.Dispose();
        }

        Buffer CreateHostBuffer(ulong size, VkBufferUsageFlags usage) {
            BufferCreateInfo info = new BufferCreateInfo {
                size = size,
                usage = usage | VkBufferUsageFlags.TransferSrcBit,
                sharingMode = VkSharingMode.Exclusive
            };

            Buffer buffer = new Buffer(renderer.Device, info);

            Allocator allocator = renderer.Memory.HostAllocator;
            Allocation alloc = allocator.Alloc(buffer.Requirements);

            buffer.Bind(alloc.memory, alloc.offset);

            return buffer;
        }

        public void CopyToBuffer(CommandBuffer commandBuffer, Buffer dest) {
            VkBufferCopy copy = new VkBufferCopy {
                size = size,
                srcOffset = 0,
                dstOffset = 0
            };

            commandBuffer.CopyBuffer(buffer, dest, copy);
        }

        public void CopyToImage(CommandBuffer commandBuffer, Image dest, uint width, uint height, uint arrayLayer) {
            VkBufferImageCopy copy = new VkBufferImageCopy {
                bufferOffset = 0,
                bufferRowLength = 0,
                bufferImageHeight = 0,
                imageSubresource = new VkImageSubresourceLayers {
                    aspectMask = VkImageAspectFlags.ColorBit,
                    mipLevel = 0,
                    baseArrayLayer = arrayLayer,
                    layerCount = 1
                },
                imageOffset = new VkOffset3D(),
                imageExtent = new VkExtent3D {
                    width = width,
                    height = height,
                    depth = 1
                }
            };

            commandBuffer.CopyBufferToImage(buffer, dest, VkImageLayout.General, copy);
        }
    }
}
