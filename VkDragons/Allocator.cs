using System;
using System.Collections.Generic;

using CSGL.Vulkan;

namespace VkDragons {
    public struct Allocation {
        public DeviceMemory memory;
        public ulong offset;
        public ulong size;
    }

    public class Allocator : IDisposable {
        struct InternalAllocation {
            public int page;
            public ulong pointer;
        }

        struct Page {
            public DeviceMemory memory;
            public ulong pointer;
            public IntPtr mapping;
        }

        Device device;

        public uint Type { get; private set; }
        ulong pageSize;
        List<Page> pages;
        Stack<InternalAllocation> stack;

        public Allocator(Device device, uint type, ulong pageSize) {
            this.device = device;
            this.Type = type;
            this.pageSize = pageSize;

            pages = new List<Page>();
            stack = new Stack<InternalAllocation>();
        }

        public void Dispose() {
            foreach (var page in pages) {
                page.memory.Dispose();
            }
        }

        public void Pop() {
            var alloc = stack.Pop();
            var page = pages[alloc.page];
            page.pointer = alloc.pointer;
            pages[alloc.page] = page;
        }

        public void Reset() {
            for (int i = 0; i < pages.Count; i++) {
                var page = pages[i];
                page.pointer = 0;
                pages[i] = page;
            }
            stack.Clear();
        }

        public Allocation Alloc(VkMemoryRequirements requirements) {
            if (requirements.size > pageSize) throw new Exception("Allocation too large");

            Allocation alloc;
            for (int i = 0; i < pages.Count; i++) {
                alloc = AttemptAlloc(i, requirements.size, requirements.alignment);
                if (alloc.memory != null) {
                    return alloc;
                }
            }

            AllocPage();
            alloc = AttemptAlloc(pages.Count - 1, requirements.size, requirements.alignment);
            if (alloc.memory != null) {
                return alloc;
            }

            throw new Exception("Could not allocate memory");
        }

        void AllocPage() {
            MemoryAllocateInfo info = new MemoryAllocateInfo {
                allocationSize = pageSize,
                memoryTypeIndex = Type
            };

            DeviceMemory memory = new DeviceMemory(device, info);

            pages.Add(new Page { memory = memory });
        }

        Allocation AttemptAlloc(int index, ulong size, ulong alignment) {
            Page page = pages[index];

            ulong unalign = page.pointer % alignment;
            ulong align = 0;

            if (unalign != 0) {
                align = alignment - unalign;
            }

            if (page.pointer + align + size > pageSize) return new Allocation();

            stack.Push(new InternalAllocation { page = index, pointer = page.pointer });

            Allocation result = new Allocation {
                memory = page.memory,
                offset = page.pointer + align,
                size = size
            };

            page.pointer += align + size;
            pages[index] = page;

            return result;
        }

        public IntPtr GetMapping(DeviceMemory memory) {
            for (int i = 0; i < pages.Count; i++) {
                var page = pages[i];
                if (page.memory != memory) continue;

                if (page.mapping != IntPtr.Zero) return page.mapping;

                page.mapping = memory.Map(0, pageSize);
                pages[i] = page;

                return page.mapping;
            }

            throw new Exception("Could not get mapping");
        }
    }
}
