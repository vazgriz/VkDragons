using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace VkDragons {
    public class Memory : IDisposable {
        Device device;
        VkPhysicalDeviceMemoryProperties memoryProperties;

        List<Allocator> deviceAllocators;
        Dictionary<DeviceMemory, Allocator> allocatorMap;

        public Allocator HostAllocator { get; private set; }

        ulong allocationSize = 128 * 1024 * 1024;

        public Memory(Device device) {
            this.device = device;
            memoryProperties = device.PhysicalDevice.MemoryProperties;
            deviceAllocators = new List<Allocator>();
            allocatorMap = new Dictionary<DeviceMemory, Allocator>();

            AllocHostMemory();
        }

        public void Dispose() {
            HostAllocator.Dispose();
            foreach (var allocator in deviceAllocators) {
                allocator.Dispose();
            }
        }

        void AllocHostMemory() {
            uint type = 0;
            bool found = false;

            VkMemoryPropertyFlags desiredProperties = VkMemoryPropertyFlags.HostVisibleBit | VkMemoryPropertyFlags.HostCoherentBit;

            for (int i = 0; i < memoryProperties.memoryTypeCount; i++) {
                if (memoryProperties.GetMemoryTypes(i).propertyFlags == desiredProperties) {
                    type = (uint)i;
                    found = true;
                    break;
                }
            }

            if (!found) {
                for (int i = 0; i < memoryProperties.memoryTypeCount; i++) {
                    if ((memoryProperties.GetMemoryTypes(i).propertyFlags & desiredProperties) != 0) {
                        type = (uint)i;
                        found = true;
                        break;
                    }
                }
            }

            if (!found) throw new Exception("Could not find suitable host memory");

            HostAllocator = new Allocator(device, type, allocationSize, allocatorMap);
        }

        Allocator AllocDevice(uint type) {
            var allocator = new Allocator(device, type, allocationSize, allocatorMap);
            deviceAllocators.Add(allocator);
            return allocator;
        }

        public Allocator GetDeviceAllocator(VkMemoryRequirements requirements) {
            foreach (var allocator in deviceAllocators) {
                uint type = allocator.Type;
                uint test = 1u << (int)type;
                if ((requirements.memoryTypeBits & test) != 0) {
                    return allocator;
                }
            }

            for (int i = 0; i < memoryProperties.memoryTypeCount; i++) {
                uint test = 1u << i;
                if ((requirements.memoryTypeBits & test) != 0 && memoryProperties.GetMemoryTypes(i).propertyFlags == VkMemoryPropertyFlags.DeviceLocalBit) {
                    return AllocDevice((uint)i);
                }
            }

            for (int i = 0; i < memoryProperties.memoryTypeCount; i++) {
                uint test = 1u << i;
                if ((requirements.memoryTypeBits & test) != 0) {
                    return AllocDevice((uint)i);
                }
            }

            throw new Exception("Could not find suitable device memory");
        }

        public Allocator GetDeviceAllocator(uint type) {
            foreach (var allocator in deviceAllocators) {
                if (allocator.Type == type) {
                    return allocator;
                }
            }

            throw new Exception("Could not find requested allocator");
        }

        public IntPtr GetMapping(DeviceMemory memory) {
            return HostAllocator.GetMapping(memory);
        }
    }
}
