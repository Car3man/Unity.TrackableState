# Unity Trackable State

Small Unity package that turns your POCO classes into trackable objects. It observes property and collection changes, and provides a single event stream with detailed path, change kind, old/new values, and optional index/key info.

Core ideas:
- Mark your POCOs with [Trackable] and call AsTrackable() to get a proxy that implements ITrackable.
- Subscribe to ITrackable.Changed to receive detailed change events.
- Use ITrackable.IsDirty and AcceptChanges() for dirty-state workflows.
- Works with nested objects and common collection interfaces: IList<T>, ISet<T>, IDictionary<TKey, TValue>.

## Features

- Trackable POCOs via attribute + proxy:
  - [Trackable] on classes
  - AsTrackable() to create a trackable proxy that implements ITrackable
- Dirty tracking:
  - IsDirty reflects if any changes happened since creation or last AcceptChanges()
  - AcceptChanges() resets IsDirty without suppressing future events
- Event model:
  - Single Changed event with ChangeEventArgs capturing:
    - Kind: ChangeKind (PropertySet, ChildChange, CollectionAdd, CollectionReplace, CollectionRemove, CollectionClear)
    - Path: dot/index path (e.g., "Name", "Inner.Description", "InnerList[2]", "Set[*]", "Dict[key]")
    - OldValue/NewValue when applicable
    - Index (for lists), Key (for dictionaries), null for sets
  - Child changes bubble up (Kind = ChildChange), with a nested Inner chain describing the original action

## Installation

- Unity Package Manager:
  - Add package from git URL (Package Manager → + → Add package from git URL…)
  - https://github.com/Car3man/Unity.TrackableState.git?path=UnityProject/Packages/com.klopoff.trackable-state
- or embed the package into Packages/ or as a local package

## Quick start

```csharp
using System.Collections.Generic;
using Klopoff.TrackableState;
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
}

public class Demo : MonoBehaviour
{
    void Start()
    {
        var root = new SampleRoot().AsTrackable();
        var t = (ITrackable)root;

        t.Changed += (_, e) =>
        {
            Debug.Log($"{e.Kind} @ {e.Path} | old={e.OldValue}, new={e.NewValue}, idx={e.Index}, key={e.Key}");
        };

        Debug.Log($"IsDirty? {t.IsDirty}");           // false
        root.Name = "John";                           // PropertySet @ Name
        Debug.Log($"IsDirty? {t.IsDirty}");           // true

        t.AcceptChanges();                            // reset IsDirty
        Debug.Log($"IsDirty? {t.IsDirty}");           // false

        root.Age = 42;                                // PropertySet @ Age -> IsDirty = true again
    }
}
```

Requirements:
- Mark classes with [Trackable] and make properties virtual so the proxy can intercept setters.
- Wrap root with AsTrackable() and use the proxy instance going forward.

## Nested objects: bubbling

```csharp
var root = new SampleRoot().AsTrackable();
var t = (ITrackable)root;

t.Changed += (_, e) => Debug.Log($"{e.Kind} @ {e.Path} | {e.OldValue} -> {e.NewValue}");

root.Inner = new SampleInner { Description = "A" };
// e.Kind = PropertySet, e.Path = "Inner"

root.Inner.Description = "B";
// e.Kind = ChildChange
// e.Path = "Inner.Description"
// e.OldValue = "A", e.NewValue = "B"
```

Replacing inner detaches the old object (its further changes won’t bubble):

```csharp
var oldInner = root.Inner;
root.Inner = new SampleInner { Description = "new" };   // PropertySet @ Inner

oldInner.Description = "changed";                       // no event
root.Inner.Description = "changed2";                    // ChildChange @ Inner.Description
```

## Collections: examples

IList<T>
```csharp
root.Tags = new List<string> { "Reading", "Traveling" };   // PropertySet @ Tags

root.Tags[0] = "Hiking";                                   // ChildChange + CollectionReplace @ Tags[0]
root.Tags.Add("Swimming");                                 // ChildChange + CollectionAdd @ Tags[2]
root.Tags.Remove("Traveling");                             // ChildChange + CollectionRemove @ Tags[1]
root.Tags.Clear();                                         // ChildChange + CollectionClear @ Tags
```

ISet<T>
```csharp
root.Set = new HashSet<string>();               // PropertySet @ Set
root.Set.Add("alpha");                          // ChildChange + CollectionAdd @ Set[*]
root.Set.Add("alpha");                          // no event (duplicate)
root.Set.Remove("alpha");                       // ChildChange + CollectionRemove @ Set[*]
root.Set.Clear();                               // ChildChange + CollectionClear @ Set
```

IDictionary<TKey, TValue>
```csharp
root.Dict = new Dictionary<string, string>();   // PropertySet @ Dict

root.Dict["en"] = "Hello";                      // ChildChange + CollectionAdd @ Dict[en]
root.Dict["en"] = "Hello2";                     // ChildChange + CollectionReplace @ Dict[en]
root.Dict.Remove("en");                         // ChildChange + CollectionRemove @ Dict[en]
root.Dict.Clear();                              // ChildChange + CollectionClear @ Dict
```

Dictionary of objects also bubbles child property changes using key addressing:
- Example path: "InnerDict[key].Description"

## Event cheatsheet

- Path formats:
  - Property: Name, Inner.Description
  - List: InnerList[0], List[2]
  - Set: Set[*]
  - Dictionary: Dict[key], InnerDict[key].Prop
- ChangeKind:
  - PropertySet
  - CollectionAdd
  - CollectionReplace
  - CollectionRemove
  - CollectionClear
  - ChildChange
- ChangeEventArgs fields you may use:
  - Kind, Path, OldValue, NewValue, Index (int?), Key (object?), Inner (nested detail for child changes)