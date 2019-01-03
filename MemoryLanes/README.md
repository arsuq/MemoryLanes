
# Memory Lanes

> v1.1

![](Logo128.jpg)

## Description 

The MemoryLanes library provides a simple API for using preallocated memory buffers, 
which could be stored on one of three locations:

* The managed heap
* The native heap
* A memory mapped file

A **MemoryLane** represents a buffer which is already allocated and can be sliced
on demand by reserving ranges in one direction only.
Consequently there is no search involved nor memory fragmentation as the exact number of bytes is blocked, as long as 
the lane has enough free space. 


A **MemoryFragment** is a GC heap object created by the MemoryLane allocation function.
It holds the starting offset and the length of the buffer slice as well as a
special destructor, injected by the Lane, which is triggered when the fragment is 
disposed. There is a common API for reading from and writing to the underlying
memory for all fragment types as well as a Span accessor.

```csharp
public abstract class MemoryFragment : IDisposable
{
	public abstract int Write(byte[] data, int offset, int length);
	public abstract int Read(byte[] destination, int offset, int destOffset = 0);
	public abstract Span<byte> Span();
	public abstract int Length { get; }
	public abstract void Dispose();
}
```

 At first it might seem that the fragment should be a structure. It's not for these reasons:

- it's very likely that the fragments will be boxed anyway (async)
- it's natural to share fragments among threads
- one could subclass a fragment
- reliable disposal, that is ref counting and resetting a lane when no Dispose is called


Similar to the thread stack deallocation, the MemoryLane cleanup is just an
offset reset, however that could only happen when there are no active fragments, 
i.e. the MemoryLane's active Fragments counter must be zero, 
which means that the lifetime of the oldest fragment determines the reset time of the lane.
  

![](MemoryLanes.png)


Due to the unpredictable fragment disposal time, using the lanes directly is not ideal.
A **MemoryCarriage** is a multi-lane allocator which is responsible for:

* allocating the requested slice on any lane, starting from the oldest to the newest 
* creating new lanes when there is no space in any of the current lanes


Depending on the actual memory storage location
one could use one of the following MemoryCarriage implementations:

* A **HeapHighway** - allocates memory on the managed heap, specifically on the Large Object Heap
if the initial capacities are greater that 80K, which is true by default (2 lanes - 8MB and 4MB)
* A **MarshalHighway** - allocates a buffer on the native heap using the Marshal.AllocHGlobal()
* A **MappedHighway** - uses a memory mapped file as a storage   

or cast them to an **IMemoryHighway** interface:

```csharp
public interface IMemoryHighway : IDisposable
{
	MemoryFragment AllocFragment(int size, int awaitMS = -1);
	int GetTotalActiveFragments();
	int GetTotalCapacity();
	int GetLanesCount();
	long LastAllocTickAnyLane { get; }
	IReadOnlyList<MemoryLane> GetLanes();
	MemoryLane this[int index] { get; }
}
```


### Reliable disposal

> From v1.1 

The current version implements two disposal modes, which could be set in the MemoryLaneSettings constructor:

- **IDispose (default)** In this mode the consumer *must* call *Dispose()* from each fragment in order 
	to reset the lane. The only other option to unfreeze a lane is to *Force()* reset it which is unsafe.

- **TrackGhosts** In order to dispose the correct number of lost fragments, each lane tracks them with 
   weak references and resets one allocation for every GC-ed and non disposed fragment. In this mode the
   consumer should still dispose despite that it's not required because the lane reset is dependent on the GC 
   as long as there is one ghost fragment. If the consumer properly disposes all fragments, this mode behaves
   as IDispiose with the overhead of the tracking.

   The *TrackGhosts* mode shifts the responsibility from Disposing to launching the cleanup function with a 
   timer or in any other way. The MemoryCarriage as well the MemoryLane classes have a *FreeGhosts()* method
   which is multi-call protected, so it's safe to call it more than once.

   In general forgetting to call Dispose on a fragment is considered a bug, however if the consumers of the
   fragments are unknown, i.e. other libraries or teams, the only safe assumption one could make
   is to expect a bad disposal.

<br>

## Usage scenarios

The original purpose for the lanes is message assembling in socket communication, which
involves fast allocation and deallocation of memory. In most cases the received bytes are 
immediately converted into a managed heap object and then discarded. With proper framing
one could make use of the different storage locations by redirecting to a heap highway 
for small messages and to a mapped highway when working with tens or hundreds of megabytes of data. 

### Consistent fragment lifetime

In cases when the lifetime of the fragments is predictable a two lane highway works fine. 
The second lane is needed when there is a very high load of concurrent allocations preventing reset by
always having at least one active fragment. When the first lane is full the MemoryCarriage will
shift the allocations to the next one, providing a small time interval of no allocations for
the first lane allowing it to reset.

### Unpredictable fragment lifetime 

In network communication there is no delivery guarantee so the two lanes initial layout would be too optimistic
regarding the fragment disposal behavior. Sometimes even small messages can be delivered 
in snail pace due to connectivity problems. In such cases having a dedicated highway per client 
is one option for reducing the probability of having a pinned lane. Another possible solution is using a highway with multiple 
short lanes, expecting long living fragments and infrequent resets, that way the amount of locked
bytes is constrained to a value that seems reasonable.

![](UnpredictableFragmentLifetime.png)

Example: Two 8M lanes could be structured as 8 2M lanes, assuming that one can
redirect larger than 2M messages to another highway, otherwise the MemoryCarriage will 
continuously append new lanes with the Min(default size, requested size), ignoring all
2M lanes. 

Alternatively to the lanes API one could use the native heap directly through the 
**MarshalSlot** class. It is a MemoryFragment thus having the same Read/Write/Span
accessors, but it is not part of any lane and doesn't affect other fragments. 


## Highway limits

Using the MemoryCarriage is somewhat similar to a stack allocation, although the space isn't fixed,
unless you configure it to be so by passing an instance of the **MemoryLaneSettings** class in the
Highway constructor.

```csharp
public class MemoryLaneSettings
{
	public Func<bool> OnMaxLaneReached;
	public Func<bool> OnMaxTotalBytesReached;

	public const int MAX_COUNT = 5000;
	public const int MIN_CAPACITY = 1023;
	public const int MAX_CAPACITY = 2_000_000_000;

	public readonly int DefaultCapacity;
	public readonly int MaxLanesCount;
	public readonly long MaxTotalAllocatedBytes;
}
```

The OnMaxLaneReached and OnMaxTotalBytesReached control whether the Highway will throw a 
MemoryLaneExcepton with codes *MaxLanesCountReached* or *MaxTotalAllocBytesReached*. 
When any of these thresholds is reached (MaxLanesCount or MaxTotalAllocatedBytes) by
default the corresponding error code is thrown. If the delegates are not null and return true
the allocation will simply fail, returning null instead of a fragment instance.

In GhostTracking disposal mode the lane will stop allocating fragments if there is no more
free tracking slots available. This number is not a setting for configuration simplicity, it's
calculated as  lane.Capacity in bytes / 32, assuming that fragments with less than 32 bytes 
will be larger than the fragment itself hence totally useless as they'll pollute the managed heap.
For example the default lane 0 in a a highway has a capacity of 8M and 8_000_000/32 = 250_000
maximum tracking slots. Note that these are not preallocated, just limited to that number. 

One may notice that the buffer lengths are limited to Int32.MaxValue everywhere 
in this API, so one couldn't use a MappedHighway with 4GB memory mapped file.
The reason is having a consistency with the Memory<T> and Span<T> implementations.


## Classes

The library adds the following classes:

In **System** namespace:

- **Fragments**
  - MemoryFragment (abstract)
  - HeapFragment
  - MappedFragment
  - MarshalFragment
  - MarshalSlot

- **Lanes**
  - MemoryLane (abstract)
  - HeapLane
  - MappedLane
  - MarshalLane
 
- **Highways**
  - MemoryCarriage (abstract)
  - IMemoryHighway (interface)
  - HeapHighway
  - MappedHighway
  - MarhsalHighway
	

- MemoryLaneSettings

- **Exceptions**
  - MemoryLaneException
  - InvariantException
  - SynchronizationException

In **System.Collections.Concurrent** namespace:

- ConcurrentArray

## Summary

Use the MemoryLanes API to relieve the GC from managing workloads that are predictable 
or simple enough to be handled manually. 
