# Unity Trackable State

Small Unity package that turns your POCO classes into trackable objects. It observes property and collection changes, and provides a single event stream with detailed path, old/new values, and optional index/key info.

Core ideas:
- Mark your POCOs with [Trackable] and call AsTrackable() to get a proxy that implements ITrackable.
- Subscribe to ITrackable.Changed to receive detailed change events.
- Use ITrackable.IsDirty and AcceptChanges() for dirty-state workflows.
- Works with nested objects and common collection interfaces: IList<T>, ISet<T>, IDictionary<TKey, TValue>.

## Features

- Zero-alloc for most cases
  - Supports to inline 24 byte structs, larger structs will be boxed
- Trackable POCOs via attribute + proxy:
  - [Trackable] on classes
  - AsTrackable() to create a trackable proxy that implements ITrackable
- Dirty tracking:
  - IsDirty reflects if any changes happened since creation or last AcceptChanges()
  - AcceptChanges() resets IsDirty without suppressing future events
- Event model:
  - Single Changed event with ChangeEventArgs capturing:
    - Full path to changed property/collection item
    - OldValue and NewValue
    - Optional Index (for IList) or Key (for IDictionary)

## Installation

- Unity Package Manager:
  - Add package from git URL (Package Manager → + → Add package from git URL…)
  - https://github.com/Car3man/Unity.TrackableState.git?path=src/Unity.TrackableState/Packages/com.klopoff.trackable-state
- or embed the package into Packages/ or as a local package

## Quick start

```csharp
using System.Collections.Generic;
using Klopoff.TrackableState.Core;
using UnityEngine;

[Trackable]
public class SampleInner
{
    public virtual string Description { get; set; }
}

[Trackable]
public class SampleRoot
{
    public virtual string Name { get; set; }
    public virtual int Age { get; set; }
    public virtual SampleInner Inner { get; set; }
    public virtual IList<string> Tags { get; set; }
    public virtual IDictionary<string, string> Properties { get; set; }
}

public class Sample : MonoBehaviour
{
    void Start()
    {
        var root = new SampleRoot
        {
            Name = "Alice",
            Age = 30,
            Inner = new SampleInner { Description = "A description" },
            Tags = new List<string> { "tag1", "tag2" },
            Properties = new Dictionary<string, string> { { "key1", "value1" }, { "key2", "value2" } }
        }.AsTrackable();

        root.Changed += static (object _, in ChangeEventArgs e) =>
        {
            Debug.Log($"{e.PathString} | old={e.oldValue}, new={e.newValue}, idx={e.index}, key={e.key}");
        };

        Debug.Log($"IsDirty? {root.IsDirty}");          // false
        root.Name = "John";                             // Name | old=Alice, new=John, idx=-1, key=
        Debug.Log($"IsDirty? {root.IsDirty}");          // true

        root.AcceptChanges();                           // reset IsDirty
        Debug.Log($"IsDirty? {root.IsDirty}");          // false

        root.Age = 42;                                  // Age | old=30, new=42, idx=-1, key=
        root.Inner.Description = "New description";     // Inner.Description | old=A description, new=New description, idx=-1, key=
        root.Tags.Add("tag3");                          // Tags[2] | old=, new=tag3, idx=2, key=
        root.Properties["key3"] = "value3";             // Properties[key3] | old=, new=value3, idx=-1, key=key3
    }
}
```

Requirements:
- Class must not be sealed
- Mark class with [Trackable] and make properties virtual so the proxy can intercept setters
- Call AsTrackable() method on class and use the proxy instance going forward
