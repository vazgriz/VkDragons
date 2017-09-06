using System;
using System.Collections.Generic;

using CSGL.GLFW;
using CSGL.Vulkan;

namespace VkDragons {
    public class Renderer : IDisposable {
        Window window;
        Instance instance;
        Surface surface;
        VkPhysicalDeviceFeatures features;
        Queue graphicsQueue;
        Queue presentQueue;
        Swapchain swapchain;
        List<Fence> fences;
        Semaphore imageAvailableSemaphore;
        Semaphore renderFinishedSemaphore;

        public PhysicalDevice PhysicalDevice { get; private set; }
        public Device Device { get; private set; }
        public CommandPool CommandPool { get; private set; }

        public IList<Image> SwapchainImages { get; private set; }
        public IList<ImageView> SwapchainImageViews { get; private set; }
        public VkFormat SwapchainFormat { get; private set; }
        public VkExtent2D SwapchainExtent { get; private set; }

        public Memory Memory { get; private set; }

        public bool VSync { get; set; } = true;

        uint imageIndex;
        public uint ImageIndex {
            get {
                return imageIndex;
            }
        }

        public bool Gamma { get; private set; }

        public int Width { get; private set; }
        public int Height { get; private set; }

        struct QueueIndices {
            public int graphicsFamily;
            public int presentFamily;

            public bool IsComplete {
                get {
                    return graphicsFamily != -1 && presentFamily != -1;
                }
            }
        }

        List<string> layers = new List<string> {
            "VK_LAYER_LUNARG_standard_validation",
	        //"VK_LAYER_LUNARG_api_dump"
        };

        List<string> extensions = new List<string> {
            "VK_KHR_swapchain"
        };

        public Renderer(Window window) {
            this.window = window;
            Width = window.FramebufferWidth;
            Height = window.FramebufferHeight;

            CreateInstance();
            CreateSurface();
            PickPhysicalDevice();
            CreateDevice();
            CreateCommandPool();
            RecreateSwapchain();
            CreateSemaphores();

            Memory = new Memory(Device);
        }

        public void Dispose() {
            Device.WaitIdle();
            Memory.Dispose();
            imageAvailableSemaphore.Dispose();
            renderFinishedSemaphore.Dispose();
            CleanupSwapchain();
            CommandPool.Dispose();
            Device.Dispose();
            surface.Dispose();
            instance.Dispose();
        }

        void CleanupSwapchain() {
            foreach (var f in fences) f.Dispose();
            foreach (var iv in SwapchainImageViews) iv.Dispose();
            swapchain.Dispose();
        }

        public void Acquire() {
            swapchain.AcquireNextImage(ulong.MaxValue, imageAvailableSemaphore, null, out imageIndex);

            var fence = fences[(int)ImageIndex];
            fence.Wait();
            fence.Reset();
        }

        public void Submit(CommandBuffer commandBuffer) {
            SubmitInfo info = new SubmitInfo {
                waitSemaphores = new List<Semaphore> { imageAvailableSemaphore },
                waitDstStageMask = new List<VkPipelineStageFlags> { VkPipelineStageFlags.ColorAttachmentOutputBit },
                commandBuffers = new List<CommandBuffer> { commandBuffer },
                signalSemaphores = new List<Semaphore> { renderFinishedSemaphore }
            };

            graphicsQueue.Submit(new List<SubmitInfo> { info }, fences[(int)ImageIndex]);
        }

        public void Present() {
            PresentInfo info = new PresentInfo {
                waitSemaphores = new List<Semaphore> { renderFinishedSemaphore },
                swapchains = new List<Swapchain> { swapchain },
                imageIndices = new List<uint> { ImageIndex }
            };

            presentQueue.Present(info);
        }

        public void Resize(int width, int height) {
            Width = width;
            Height = height;

            Device.WaitIdle();
            CleanupSwapchain();
            RecreateSwapchain();
        }

        public CommandBuffer GetSingleUseCommandBuffer() {
            CommandBuffer commandBuffer = CommandPool.Allocate(VkCommandBufferLevel.Primary);

            commandBuffer.Begin(new CommandBufferBeginInfo {
                flags = VkCommandBufferUsageFlags.OneTimeSubmitBit
            });

            return commandBuffer;
        }

        public void SubmitCommandBuffer(CommandBuffer commandBuffer) {
            commandBuffer.End();

            SubmitInfo info = new SubmitInfo {
                commandBuffers = new List<CommandBuffer> {
                    commandBuffer
                }
            };

            graphicsQueue.Submit(new List<SubmitInfo> { info });
            graphicsQueue.WaitIdle();

            CommandPool.Free(new List<CommandBuffer> { commandBuffer });
        }

        void CreateInstance() {
            var extensions = new List<string>(GLFW.GetRequiredInstanceExceptions());
            InstanceCreateInfo info = new InstanceCreateInfo {
                extensions = extensions,
                applicationInfo = new ApplicationInfo {
                    apiVersion = new VkVersion(1, 0, 0),
                    applicationName = "Here Be Dragons",
                    applicationVersion = new VkVersion(1, 0, 0),
                    engineVersion = new VkVersion(1, 0, 0)
                }
            };

            if (CheckValidationSupport(layers)) {
                info.layers = layers;
            }

            instance = new Instance(info);
        }

        bool CheckValidationSupport(List<string> layers) {
            var availableLayers = Instance.AvailableLayers;

            foreach (var layer in layers) {
                bool layerFound = false;

                foreach (var available in availableLayers) {
                    if (available.Name == layer) {
                        layerFound = true;
                        break;
                    }
                }

                if (!layerFound) {
                    return false;
                }
            }

            return true;
        }

        void CreateSurface() {
            surface = new Surface(instance, window);
        }

        void PickPhysicalDevice() {
            if (instance.PhysicalDevices.Count == 0) {
                throw new Exception("Could not find GPU with Vulkan support");
            }

            foreach (var candidate in instance.PhysicalDevices) {
                if (IsDeviceSuitable(candidate)) {
                    PhysicalDevice = candidate;
                    break;
                }
            }

            if (PhysicalDevice == null) {
                throw new Exception("Could not find suitable GPU");
            }

            Console.WriteLine(PhysicalDevice.Name);
        }

        bool IsDeviceSuitable(PhysicalDevice physicalDevice) {
            var indices = GetIndices(physicalDevice);

            bool extensionsSupported = CheckExtensionSupport(physicalDevice);

            var modes = surface.GetModes(physicalDevice);
            var formats = surface.GetFormats(physicalDevice);

            return indices.IsComplete && extensionsSupported && modes.Count > 0 && formats.Count > 0;
        }

        QueueIndices GetIndices(PhysicalDevice physicalDevice) {
            QueueIndices results = new QueueIndices {
                graphicsFamily = -1,
                presentFamily = -1
            };

            for (int i = 0; i < physicalDevice.QueueFamilies.Count; i++) {
                var family = physicalDevice.QueueFamilies[i];

                if (family.QueueCount > 0 && (family.Flags & VkQueueFlags.GraphicsBit) != 0) {
                    results.graphicsFamily = i;
                }

                if (family.QueueCount > 0 && family.SurfaceSupported(surface)) {
                    results.presentFamily = i;
                }

                if (results.IsComplete) {
                    break;
                }
            }

            return results;
        }

        bool CheckExtensionSupport(PhysicalDevice physicalDevice) {
            foreach (var ex in extensions) {
                bool found = false;

                foreach (var available in physicalDevice.AvailableExtensions) {
                    if (available.Name == ex) {
                        found = true;
                        break;
                    }
                }

                if (!found) return false;
            }

            return true;
        }

        void CreateDevice() {
            var indices = GetIndices(PhysicalDevice);

            List<DeviceQueueCreateInfo> infos = new List<DeviceQueueCreateInfo>();
            HashSet<int> uniqueFamilies = new HashSet<int> { indices.graphicsFamily, indices.presentFamily };

            List<float> priorities = new List<float> { 1f };
            foreach (var family in uniqueFamilies) {
                infos.Add(new DeviceQueueCreateInfo {
                    queueFamilyIndex = (uint)family,
                    queueCount = 1,
                    priorities = priorities
                });
            }

            SelectFeatures();

            DeviceCreateInfo info = new DeviceCreateInfo {
                extensions = extensions,
                features = features,
                queueCreateInfos = infos
            };

            Device = new Device(PhysicalDevice, info);

            graphicsQueue = Device.GetQueue((uint)indices.graphicsFamily, 0);
            presentQueue = Device.GetQueue((uint)indices.presentFamily, 0);
        }

        void SelectFeatures() {
            var available = PhysicalDevice.Features;

            if (available.shaderClipDistance == 1) {
                features.shaderClipDistance = 1;
            }
            if (available.shaderCullDistance == 1) {
                features.shaderClipDistance = 1;
            }
        }

        void CreateCommandPool() {
            var indices = GetIndices(PhysicalDevice);

            CommandPoolCreateInfo info = new CommandPoolCreateInfo {
                flags = VkCommandPoolCreateFlags.ResetCommandBufferBit,
                queueFamilyIndex = (uint)indices.graphicsFamily
            };

            CommandPool = new CommandPool(Device, info);
        }

        void RecreateSwapchain() {
            CreateSwapchain();
            CreateImageViews();
            CreateFences();
        }

        VkSurfaceFormatKHR ChooseSurfaceFormat(List<VkSurfaceFormatKHR> availableFormats) {
            if (availableFormats.Count == 1 && availableFormats[0].format == VkFormat.Undefined) {
                Gamma = true;
                return new VkSurfaceFormatKHR {
                    format = VkFormat.R8g8b8a8Unorm,
                    colorSpace = VkColorSpaceKHR.SrgbNonlinearKhr
                };
            }

            foreach (var availableFormat in availableFormats) {
                if ((availableFormat.format == VkFormat.R8g8b8a8Srgb || availableFormat.format == VkFormat.B8g8r8a8Srgb)
                    && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonlinearKhr) {
                    Gamma = true;
                    return availableFormat;
                }
            }

            foreach (var availableFormat in availableFormats) {
                if ((availableFormat.format == VkFormat.R8g8b8a8Unorm || availableFormat.format == VkFormat.B8g8r8a8Unorm)
                    && availableFormat.colorSpace == VkColorSpaceKHR.SrgbNonlinearKhr) {
                    Gamma = false;
                    return availableFormat;
                }
            }

            throw new Exception("Could not find suitable surface format");
        }

        VkPresentModeKHR ChoosePresentMode(List<VkPresentModeKHR> availablePresentModes) {
            VkPresentModeKHR bestMode = VkPresentModeKHR.FifoKhr;

            if (!VSync) {
                foreach (var availablePresentMode in availablePresentModes) {
                    if (availablePresentMode == VkPresentModeKHR.ImmediateKhr) {
                        bestMode = availablePresentMode;
                    } else if (availablePresentMode == VkPresentModeKHR.MailboxKhr && bestMode == VkPresentModeKHR.FifoKhr) {
                        bestMode = availablePresentMode;
                    }
                }
            }

            return bestMode;
        }

        VkExtent2D ChooseSwapExtent(VkSurfaceCapabilitiesKHR capabilities) {
            if (capabilities.currentExtent.width != uint.MaxValue) {
                return capabilities.currentExtent;
            } else {
                uint width = (uint)Width;
                uint height = (uint)Height;

                width = Math.Max(capabilities.minImageExtent.width, Math.Min(capabilities.maxImageExtent.width, width));
                height = Math.Max(capabilities.minImageExtent.height, Math.Min(capabilities.maxImageExtent.height, height));

                return new VkExtent2D { width = (uint)width, height = (uint)height };
            }
        }

        void CreateSwapchain() {
            var capabilities = surface.GetCapabilities(PhysicalDevice);
            var formats = surface.GetFormats(PhysicalDevice);
            var modes = surface.GetModes(PhysicalDevice);

            VkSurfaceFormatKHR format = ChooseSurfaceFormat(formats);
            VkPresentModeKHR mode = ChoosePresentMode(modes);
            VkExtent2D extent = ChooseSwapExtent(capabilities);

            uint imageCount;
            if (mode == VkPresentModeKHR.MailboxKhr) {
                imageCount = 3;
            } else {
                imageCount = 2;
            }

            if (capabilities.maxImageCount > 0 && imageCount > capabilities.maxImageCount) {
                imageCount = capabilities.maxImageCount;
            }

            SwapchainCreateInfo info = new SwapchainCreateInfo();
            info.surface = surface;
            info.minImageCount = imageCount;
            info.imageFormat = format.format;
            info.imageColorSpace = format.colorSpace;
            info.imageExtent = extent;
            info.imageArrayLayers = 1;
            info.imageUsage = VkImageUsageFlags.ColorAttachmentBit;

            var indices = GetIndices(PhysicalDevice);

            if (indices.graphicsFamily != indices.presentFamily) {
                info.imageSharingMode = VkSharingMode.Concurrent;
                info.queueFamilyIndices = new List<uint> { (uint)indices.graphicsFamily, (uint)indices.presentFamily };
            } else {
                info.imageSharingMode = VkSharingMode.Exclusive;
            }

            info.preTransform = capabilities.currentTransform;
            info.compositeAlpha = VkCompositeAlphaFlagsKHR.OpaqueBitKhr;
            info.presentMode = mode;
            info.clipped = true;

            swapchain = new Swapchain(Device, info);

            SwapchainImages = swapchain.Images;
            SwapchainFormat = swapchain.Format;
            SwapchainExtent = swapchain.Extent;
        }

        void CreateImageViews() {
            SwapchainImageViews = new List<ImageView>(SwapchainImages.Count);

            for (int i = 0; i < SwapchainImages.Count; i++) {
                ImageViewCreateInfo info = new ImageViewCreateInfo {
                    image = SwapchainImages[i],
                    viewType = VkImageViewType._2d,
                    format = SwapchainFormat,
                    components = new VkComponentMapping {
                        r = VkComponentSwizzle.Identity,
                        g = VkComponentSwizzle.Identity,
                        b = VkComponentSwizzle.Identity,
                        a = VkComponentSwizzle.Identity
                    },
                    subresourceRange = new VkImageSubresourceRange {
                        aspectMask = VkImageAspectFlags.ColorBit,
                        baseMipLevel = 0,
                        levelCount = 1,
                        baseArrayLayer = 0,
                        layerCount = 1
                    }
                };

                SwapchainImageViews.Add(new ImageView(Device, info));
            }
        }

        void CreateFences() {
            fences = new List<Fence>(SwapchainImages.Count);

            for (int i = 0; i < SwapchainImages.Count; i++) {
                FenceCreateInfo info = new FenceCreateInfo {
                    Flags = VkFenceCreateFlags.SignaledBit
                };

                fences.Add(new Fence(Device, info));
            }
        }

        void CreateSemaphores() {
            imageAvailableSemaphore = new Semaphore(Device);
            renderFinishedSemaphore = new Semaphore(Device);
        }
    }
}
