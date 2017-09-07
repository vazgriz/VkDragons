using System;
using System.Collections.Generic;
using System.Numerics;
using System.IO;

using CSGL;
using CSGL.Vulkan;
using Image = CSGL.Vulkan.Image;
using CSGL.STB;

namespace VkDragons {
    public enum TextureType {
        Image,
        Cubemap,
        Depth
    }

    public class Texture : IDisposable {
        Renderer renderer;
        Image image;
        Allocation alloc;

        List<List<byte>> data;
        List<Vector2i> mipChain;
        uint mipLevels;
        uint arrayLayers;

        public VkFormat Format { get; private set; }
        public uint Width { get; private set; }
        public uint Height { get; private set; }
        public ImageView ImageView { get; private set; }

        public Texture(Renderer renderer, TextureType type, string filename, bool gammaSpace = false) {
            this.renderer = renderer;
            mipChain = new List<Vector2i>();
            switch (type) {
                case TextureType.Image:
                    Init(filename, gammaSpace);
                    break;
                case TextureType.Cubemap:
                    InitCubemap(filename, gammaSpace);
                    break;
                default:
                    throw new Exception("Unsupported");
            }
        }

        public Texture(Renderer renderer, TextureType type, uint width, uint height, VkImageUsageFlags usage, VkFormat format = VkFormat.Undefined) {
            this.renderer = renderer;
            mipChain = new List<Vector2i>();
            switch (type) {
                case TextureType.Image:
                    Init(width, height, format, usage);
                    break;
                case TextureType.Depth:
                    InitDepth(width, height, usage);
                    break;
                default:
                    throw new Exception("Unsupported");
            }
        }

        public void Dispose() {
            renderer.Memory.Free(alloc);
            image.Dispose();
            ImageView.Dispose();
        }

        void Init(string filename, bool gammaSpace) {
            LoadImages(new List<string> { filename });
            CalculateMipChain();

            if (gammaSpace) {
                Format = VkFormat.R8g8b8a8Srgb;
            } else {
                Format = VkFormat.R8g8b8a8Unorm;
            }

            CreateImage(VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.SampledBit,
                VkImageCreateFlags.None);
            CreateImageView(VkImageAspectFlags.ColorBit, VkImageViewType._2d);
        }

        void InitCubemap(string filenameRoot, bool gammaSpace) {
            List<string> filenames = new List<string> {
                filenameRoot + "_r.png",
		        filenameRoot + "_l.png",
		        filenameRoot + "_d.png",
		        filenameRoot + "_u.png",
		        filenameRoot + "_b.png",
		        filenameRoot + "_f.png",
	        };

            LoadImages(filenames);
            CalculateMipChain();

            if (gammaSpace) {
                Format = VkFormat.R8g8b8a8Srgb;
            } else {
                Format = VkFormat.R8g8b8a8Unorm;
            }

            CreateImage(VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.SampledBit,
                VkImageCreateFlags.CubeCompatibleBit);
            CreateImageView(VkImageAspectFlags.ColorBit, VkImageViewType._2dArray);
        }

        void Init(uint width, uint height, VkFormat format, VkImageUsageFlags usage) {
            mipLevels = 1;
            arrayLayers = 1;
            Format = format;
            Width = width;
            Height = height;

            CreateImage(VkImageUsageFlags.TransferDstBit | VkImageUsageFlags.TransferSrcBit | VkImageUsageFlags.SampledBit | usage,
                VkImageCreateFlags.None);
            CreateImageView(VkImageAspectFlags.ColorBit, VkImageViewType._2d);
        }

        void InitDepth(uint width, uint height, VkImageUsageFlags usage) {
            mipLevels = 1;
            arrayLayers = 1;
            Width = width;
            Height = height;
            Format = FindDepthFormat();

            CreateImage(VkImageUsageFlags.DepthStencilAttachmentBit | usage, VkImageCreateFlags.None);
            CreateImageView(VkImageAspectFlags.DepthBit, VkImageViewType._2d);
        }

        void LoadImages(List<string> filenames) {
            data = new List<List<byte>>(filenames.Count);
            int width = 0;
            int height = 0;
            int components;

            for (int i = 0; i < filenames.Count; i++) {
                var raw = File.ReadAllBytes(filenames[i]);
                data.Add(new List<byte>(STB.Load(raw, out width, out height, out components, 4)));
            }

            Width = (uint)width;
            Height = (uint)height;
            arrayLayers = (uint)filenames.Count;
        }

        public void UploadData(CommandBuffer commandBuffer, DisposableList<StagingBuffer> stagingBuffers) {

        }

        void CreateImage(VkImageUsageFlags usage, VkImageCreateFlags flags) {
            ImageCreateInfo info = new ImageCreateInfo {
                imageType = VkImageType._2d,
                format = Format,
                extent = new VkExtent3D {
                    width = Width,
                    height = Height,
                    depth = 1
                },
                mipLevels = mipLevels,
                arrayLayers = arrayLayers,
                tiling = VkImageTiling.Optimal,
                initialLayout = VkImageLayout.Undefined,
                sharingMode = VkSharingMode.Exclusive,
                samples = VkSampleCountFlags._1Bit,
                usage = usage,
                flags = flags
            };

            image = new Image(renderer.Device, info);

            Allocator allocator = renderer.Memory.GetDeviceAllocator(image.Requirements);
            alloc = allocator.Alloc(image.Requirements);

            image.Bind(alloc.memory, alloc.offset);
        }

        void CreateImageView(VkImageAspectFlags aspect, VkImageViewType viewType) {
            ImageViewCreateInfo info = new ImageViewCreateInfo {
                image = image,
                format = Format,
                viewType = viewType,
                components = new VkComponentMapping {
                    r = VkComponentSwizzle.Identity,
                    g = VkComponentSwizzle.Identity,
                    b = VkComponentSwizzle.Identity,
                    a = VkComponentSwizzle.Identity
                },
                subresourceRange = new VkImageSubresourceRange {
                    aspectMask = aspect,
                    baseArrayLayer = 0,
                    layerCount = arrayLayers,
                    baseMipLevel = 0,
                    levelCount = mipLevels
                }
            };

            ImageView = new ImageView(renderer.Device, info);
        }

        void CalculateMipChain() {
            int w = (int)Width;
            int h = (int)Height;

            while (w != 1 && h != 1) {
                mipChain.Add(new Vector2i(w, h));
                if (w > 1) w /= 2;
                if (h > 1) h /= 2;
            }

            mipLevels = (uint)mipChain.Count;
        }

        void GenerateMipChain(CommandBuffer commandBuffer) {
            if (mipChain.Count == 1) return;

            for (int i = 1; i < mipChain.Count; i++) {
                var src = mipChain[i - 1];
                var dst = mipChain[i];

                VkImageBlit blit = new VkImageBlit {
                    srcSubresource = new VkImageSubresourceLayers {
                        aspectMask = VkImageAspectFlags.ColorBit,
                        baseArrayLayer = 0,
                        layerCount = (uint)data.Count,
                        mipLevel = (uint)(i - 1)
                    },
                    srcOffsets_0 = new VkOffset3D {
                        x = 0,
                        y = 0,
                        z = 0,
                    },
                    srcOffsets_1 = new VkOffset3D {
                        x = src.X,
                        y = src.Y,
                        z = 1
                    },
                    dstSubresource = new VkImageSubresourceLayers {
                        aspectMask = VkImageAspectFlags.ColorBit,
                        baseArrayLayer = 0,
                        layerCount = (uint)data.Count,
                        mipLevel = (uint)i
                    },
                    dstOffsets_0 = new VkOffset3D {
                        x = 0,
                        y = 0,
                        z = 0
                    },
                    dstOffsets_1 = new VkOffset3D {
                        x = dst.X,
                        y = dst.Y,
                        z = 1
                    }
                };

                commandBuffer.BlitImage(image, VkImageLayout.General, image, VkImageLayout.General, blit, VkFilter.Linear);

                Transition(commandBuffer, VkImageLayout.General, VkImageLayout.General);
            }
        }

        bool HasStencil(VkFormat format) {
            return format == VkFormat.D32SfloatS8Uint || format == VkFormat.D24UnormS8Uint;
        }

        void Transition(CommandBuffer commandBuffer, VkImageLayout oldLayout, VkImageLayout newLayout) {
            ImageMemoryBarrier barrier = new ImageMemoryBarrier();
            barrier.image = image;
            barrier.oldLayout = oldLayout;
            barrier.newLayout = newLayout;
            barrier.srcQueueFamilyIndex = uint.MaxValue;    //VK_QUEUE_FAMILY_IGNORED
            barrier.dstQueueFamilyIndex = uint.MaxValue;

            if (newLayout == VkImageLayout.DepthStencilAttachmentOptimal) {
                barrier.subresourceRange.aspectMask = VkImageAspectFlags.DepthBit;

                if (HasStencil(Format)) {
                    barrier.subresourceRange.aspectMask |= VkImageAspectFlags.StencilBit;
                }
            } else {
                barrier.subresourceRange.aspectMask = VkImageAspectFlags.ColorBit;
            }

            barrier.subresourceRange.baseMipLevel = 0;
            barrier.subresourceRange.levelCount = mipLevels;
            barrier.subresourceRange.baseArrayLayer = 0;
            barrier.subresourceRange.layerCount = arrayLayers;

            VkPipelineStageFlags source;
            VkPipelineStageFlags dest;

            if (oldLayout == VkImageLayout.Undefined && (newLayout == VkImageLayout.TransferDstOptimal || newLayout == VkImageLayout.General)) {
                barrier.srcAccessMask = VkAccessFlags.None;
                barrier.dstAccessMask = VkAccessFlags.TransferWriteBit;

                source = VkPipelineStageFlags.TopOfPipeBit;
                dest = VkPipelineStageFlags.TransferBit;
            } else if ((oldLayout == VkImageLayout.TransferDstOptimal || oldLayout == VkImageLayout.General) && newLayout == VkImageLayout.ShaderReadOnlyOptimal) {
                barrier.srcAccessMask = VkAccessFlags.TransferWriteBit;
                barrier.dstAccessMask = VkAccessFlags.ShaderReadBit;

                source = VkPipelineStageFlags.TransferBit;
                dest = VkPipelineStageFlags.FragmentShaderBit;
            } else if (oldLayout == VkImageLayout.Undefined && newLayout == VkImageLayout.DepthStencilAttachmentOptimal) {
                barrier.srcAccessMask = VkAccessFlags.None;
                barrier.dstAccessMask = VkAccessFlags.DepthStencilAttachmentReadBit | VkAccessFlags.DepthStencilAttachmentWriteBit;

                source = VkPipelineStageFlags.TopOfPipeBit;
                dest = VkPipelineStageFlags.EarlyFragmentTestsBit;
            } else if (oldLayout == VkImageLayout.General && newLayout == VkImageLayout.General) {
                barrier.srcAccessMask = VkAccessFlags.TransferWriteBit | VkAccessFlags.TransferReadBit;
                barrier.dstAccessMask = VkAccessFlags.TransferWriteBit | VkAccessFlags.TransferReadBit;

                source = VkPipelineStageFlags.TransferBit;
                dest = VkPipelineStageFlags.TransferBit;
            } else {
                throw new Exception("Unsupported");
            }

            commandBuffer.PipelineBarrier(source, dest, VkDependencyFlags.None, null, null, new List<ImageMemoryBarrier> { barrier });
        }

        VkFormat FindSupportedFormat(List<VkFormat> candidates, VkFormatFeatureFlags features) {
            foreach (var format in candidates) {
                VkFormatProperties props = renderer.PhysicalDevice.GetFormatProperties(format);

                if ((props.optimalTilingFeatures & features) == features) {
                    return format;
                }
            }

            throw new Exception("Could not find supported format");
        }

        VkFormat FindDepthFormat() {
            return FindSupportedFormat(
                new List<VkFormat> { VkFormat.D32Sfloat, VkFormat.D32SfloatS8Uint, VkFormat.D24UnormS8Uint },
                VkFormatFeatureFlags.DepthStencilAttachmentBit);
        }
    }
}
