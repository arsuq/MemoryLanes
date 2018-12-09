# Tests

This Console app contains tests for the *MemoryLanes* library.
The Surface folder contains the entry points of the use case
runners, which implement the ITestSurface interface and provide a
Run(string[] args) method for launching the specific test.
The common testing functionality is placed in the Internals folder.

**To run a specific test surface:**

``` 
> dotnet Tests.dll -SurfaceClassName -args param1 param2
```

**Run everything:**

```
> dotnet Tests.dll -all
```

**How to create a new test**

The TestRunner loads all classes implementing the ITestSurface interface 
and launches the Run method of:

- a specific test class if the *-ClassName* is provided as an argument and the ClassName 
 class exists 
- every ITestSurface implementation which has default arguments, 
 i.e. RequireArgs = false; this is the -all switch case  

The ITestSurface has the following properties:

``` csharp
string FailureMessage // Set for details if the test fails 
bool? Passed // If not set the runner will trace the test as unknown
bool RequireArgs // False if there are default values to test with
string Info // The description of the test, including the switches 
```

the test launcher

```csharp
// Keys = switches, the list has the values after the switch, if any
Task Run(IDictionary<string, List<string>> args);
```