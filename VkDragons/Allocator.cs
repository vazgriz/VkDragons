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
        struct Node {
            public ulong offset;
            public ulong size;
        }

        struct Page {
            public DeviceMemory memory;
            public LinkedList<Node> nodes;
            public IntPtr mapping;
        }

        Device device;

        public uint Type { get; private set; }
        ulong pageSize;
        List<Page> pages;
        Dictionary<DeviceMemory, int> pageMap;

        Dictionary<DeviceMemory, Allocator> allocatorMap;

        public Allocator(Device device, uint type, ulong pageSize, Dictionary<DeviceMemory, Allocator> allocatorMap) {
            this.device = device;
            this.pageSize = pageSize;
            Type = type;

            pages = new List<Page>();
            pageMap = new Dictionary<DeviceMemory, int>();

            this.allocatorMap = allocatorMap;
        }

        public void Dispose() {
            foreach (var page in pages) {
                page.memory.Dispose();
            }
        }

        public void Reset() {
            for (int i = 0; i < pages.Count; i++) {
                var page = pages[i];
                pages[i].nodes.Clear();
                pages[i].nodes.AddFirst(new Node { offset = 0, size = pageSize });
            }
        }

        public Allocation Alloc(VkMemoryRequirements requirements) {
            if (requirements.size > pageSize) throw new Exception("Allocation too large");

            Allocation alloc;
            for (int i = 0; i < pages.Count; i++) {
                alloc = AttemptAlloc(pages[i], requirements);
                if (alloc.memory != null) {
                    return alloc;
                }
            }

            AllocPage();
            alloc = AttemptAlloc(pages[pages.Count - 1], requirements);
            if (alloc.memory != null) {
                return alloc;
            }

            throw new Exception("Could not allocate memory");
        }

        public void Free(Allocation alloc) {
            Page page = GetPage(alloc.memory);

            var node = page.nodes.First;
            while (node != null) {
                if (node.Value.offset > alloc.offset) {
                    page.nodes.AddBefore(node, new Node { offset = alloc.offset, size = alloc.size });
                    CombineNodes(page.nodes, node);
                }
                node = node.Next;
            }
        }

        void AllocPage() {
            MemoryAllocateInfo info = new MemoryAllocateInfo {
                allocationSize = pageSize,
                memoryTypeIndex = Type
            };

            DeviceMemory memory = new DeviceMemory(device, info);

            pages.Add(new Page { memory = memory, nodes = new LinkedList<Node>() });
            pages[pages.Count - 1].nodes.AddFirst(new Node { offset = 0, size = pageSize });
            pageMap.Add(memory, pages.Count - 1);
            allocatorMap.Add(memory, this);
        }

        Allocation AttemptAlloc(Page page, VkMemoryRequirements requirements) {
            var node = page.nodes.First;

            while (node != null) {
                if (node.Value.size >= requirements.size) {
                    Allocation result = AttemptAlloc(page, node, requirements);
                    if (result.memory != null) {
                        SplitNode(page.nodes, node, result);
                        return result;
                    }
                }
                node = node.Next;
            }

            return new Allocation();
        }

        Allocation AttemptAlloc(Page page, LinkedListNode<Node> node, VkMemoryRequirements requirements) {
            ulong unalign = node.Value.offset % requirements.alignment;
            ulong align = 0;

            if (unalign != 0) {
                align = requirements.alignment - unalign;
            }

            if (node.Value.offset + align + requirements.size > pageSize) return new Allocation();

            Allocation result = new Allocation {
                memory = page.memory,
                offset = node.Value.offset + align,
                size = requirements.size
            };
            
            return result;
        }

        void SplitNode(LinkedList<Node> list, LinkedListNode<Node> node, Allocation alloc) {
            ulong frontSlack = alloc.offset - node.Value.offset;
            ulong endSlack = (node.Value.offset + node.Value.size) - (alloc.offset + alloc.size);

            if (frontSlack == 0 && endSlack == 0) {
                list.Remove(node);
            } else if (frontSlack == 0 && endSlack > 0) {
                var value = node.Value;
                value.offset = alloc.offset + alloc.size;
                value.size = endSlack;
                node.Value = value;
            } else if (frontSlack > 0 && endSlack == 0) {
                var value = node.Value;
                value.size = frontSlack;
                node.Value = value;
            } else {
                list.AddBefore(node, new Node { offset = node.Value.offset, size = frontSlack });
                var value = node.Value;
                value.offset = alloc.offset + alloc.size;
                value.size = endSlack;
                node.Value = value;
            }
        }

        void CombineNodes(LinkedList<Node> list, LinkedListNode<Node> node) {
            var middle = node.Previous;
            CombineNode(list, node);
            CombineNode(list, middle);
        }

        void CombineNode(LinkedList<Node> list, LinkedListNode<Node> node) {
            var prev = node.Previous;
            if (prev != null && prev.Value.offset + prev.Value.size == node.Value.offset) {
                var value = prev.Value;
                value.size += node.Value.size;
                prev.Value = value;
                list.Remove(node);
            }
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

        Page GetPage(DeviceMemory memory) {
            if (pageMap.ContainsKey(memory)) {
                return pages[pageMap[memory]];
            }

            throw new Exception("Could not find page");
        }
    }
}
