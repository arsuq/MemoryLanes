# Tests

This Console app contains tests for the *MemoryLanes* library.
The Surface folder contains the entry points of the use case
runners, which implement the ITestSurface interface and provide a
Run(string[] args) method for launching the specific test.
The common testing functionality is placed in the Internals folder.

**To run a specific test surface:**

``` 
> dotnet Tests.dll +SurfaceClassName -args param1 param2
```

**Run everything:**

```
> dotnet Tests.dll +all
```
