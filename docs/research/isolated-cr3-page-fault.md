# Why an Isolated CR3 Caused a Page Fault in SAIOS

> A kernel debugging case study: tracing a user-mode page fault from CR3 through the page-table walk to the address-space ownership problem.

## Status

**Research / historical failure analysis.** This document records the debugging method and the architectural lesson. It does not claim that every implementation detail described here is the current SAIOS design.

## The failure

During the transition from a shared/identity-mapped bring-up path to an isolated process address space, a ring-3 execution path could enter successfully and then fault on a user virtual address.

A representative diagnostic looked like:

```text
Page Fault (error=0x2 cr2=0x806000 sp=0x3ffffaf0)
CR3=0x101000
pml4e=0x0000000000102023
pdpte=0x0000000000104023
pde=0x00000000026a4063 huge=0
pte=0x0000000000000000
```

The important observation was not merely that the PTE was zero. The fault had to be interpreted in the context of **which address space was active when the access occurred**.

## What the CPU told us

The page-fault error code was `0x2`:

- bit 0 clear: the access was reported as a non-present-page fault
- bit 1 set: the access was a write

`CR2=0x806000` identified the linear address that could not be translated with the permissions available in the active address space.

The page-table walk showed valid upper levels but no present PTE for the final translation.

That immediately narrowed the investigation to two classes of causes:

1. the page was never mapped into the process address space, or
2. the mapping existed in another address space but the CPU was executing under a different CR3.

## The misleading symptom

The early kernel bring-up path had relied heavily on low-memory identity mappings. That made physical addresses usable as pointers while the current page tables happened to cover the same range.

Once isolated CR3s were introduced, that assumption stopped being valid.

A physical page returned by the physical-memory manager is **not automatically a valid virtual address in the currently active address space**.

This distinction is fundamental:

```text
physical address != virtual address

unless the active address space explicitly maps them together.
```

## The investigation

The debugging sequence was deliberately kept from the fault outward:

```text
faulting RIP / CR2 / RSP
        |
        v
active CR3
        |
        v
PML4 -> PDPT -> PD -> PT
        |
        v
permissions + presence
        |
        v
who created the mapping?
        |
        v
which address space was active?
```

The critical question became:

> Was the mapping being created in the same address space that the user process was actually running under?

This prevented the investigation from turning into random page-table edits.

## Root architectural lesson

An isolated process address space creates a hard contract:

> Every pointer used while that address space is active must be valid under that address space, unless the kernel deliberately switches to another address space or maintains a documented shared mapping.

That applies not only to user memory, but also to kernel code and data touched during system-call and exception paths.

The dangerous pattern is conceptually:

```text
physical_page = PMM.allocate()
zero(physical_page)          // unsafe if physical_page is not mapped
map(process_as, virtual, physical_page)
```

The safe design must establish a valid virtual mapping before dereferencing the physical page:

```text
physical_page = PMM.allocate()
table_va = kernel_map(physical_page)
zero(table_va)
unmap_or_retain(table_va)
map(process_as, virtual, physical_page)
```

The exact mechanism can differ, but the invariant cannot.

## Why identity mapping hid the bug

Identity mapping is useful during early kernel bring-up because it removes one translation layer from debugging.

It is also dangerous when code begins to assume that every physical address is a usable pointer.

A system can therefore pass early tests while containing a latent address-space bug:

```text
bring-up CR3
  -> low physical addresses happen to be mapped
  -> physical-as-pointer appears to work

isolated process CR3
  -> same physical address is not mapped
  -> page fault
```

The transition to isolated address spaces therefore needs to be treated as an architectural boundary, not merely a paging feature.

## Regression strategy

A useful regression suite should deliberately test both sides of the boundary:

### 1. Kernel-table allocation

Allocate a page-table page whose physical address is outside the identity-mapped region. Verify that table initialization succeeds through an explicit kernel mapping.

### 2. Process isolation

Create two address spaces and map the same virtual address differently. Verify that switching CR3 changes the observed translation as expected.

### 3. User mapping ownership

Map a user page into process A only. Switch to process B and verify that the same virtual access faults rather than silently reaching process A's memory.

### 4. Fault diagnostics

On a deliberate non-present access, record at minimum:

```text
CR2
CR3
RIP
RSP
page-fault error code
PML4E
PDPTE
PDE
PTE
```

This turns a crash into evidence.

## Engineering rule extracted from the incident

**Never dereference a physical address merely because it was returned by the PMM.**

A physical page must first be made reachable through a virtual mapping valid under the current execution context.

That rule belongs at the memory/VMM contract boundary, not as a convention remembered by individual callers.

## Why this matters beyond SAIOS

The same failure class appears in real operating-system work whenever code moves from a shared address space to isolated address spaces:

- page-table construction
- process creation
- `execve`
- kernel/user transitions
- copy-on-write
- DMA buffer handling
- interrupt-time memory access
- temporary physical mappings

The general lesson is simple:

> **Address-space isolation exposes assumptions that identity mapping was hiding.**

## Evidence discipline

This case study intentionally separates historical diagnostic evidence from current implementation claims. A future update should add exact SAIOS commit/file references when the corresponding implementation is stable and publicly attributable.

That distinction matters. A useful debugging document should preserve what was actually observed without pretending an old workaround is still the final architecture.

## Next investigation

The next high-value SAIOS case study should trace the **ELF → user stack → `_start` → first syscall** path and document where ABI assumptions cross the kernel/user boundary.
