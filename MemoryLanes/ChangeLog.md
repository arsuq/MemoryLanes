
# Changes

## v2.2

+ A VirtualHighway added. 

## v2.1

+ A HeapSlot MemoryFragment type added.

## v2.0

+ The Ghost tracking disposal mode is removed.
+ The allocations are serialized with monitor.TryEnter and a timeout, so 
  one could wait if data locality is desired or use awaitMS = 0 and skip a 
  lane if the latency is more important.
+ The MAX_LANE_COUNT is one million slots.
+ All lane and fragment types have multi-call protected Dispose().
+ The highways can Dispose/Reopen Lanes
+ MemoryLane.GetAllBytes diagnostic method 

