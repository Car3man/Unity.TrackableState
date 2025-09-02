using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;

namespace Klopoff.TrackableState.Tests
{
    [Trackable]
    public class SampleRoot
    {
        public virtual string Name { get; set; }
        public virtual int Age { get; set; }
        public virtual SampleInner Inner { get; set; }
        public virtual IList<SampleInner> InnerList { get; set; }
        public virtual IList<string> List { get; set; }
        public virtual ISet<SampleInner> InnerSet { get; set; }
        public virtual ISet<string> Set { get; set; }
        public virtual IDictionary<string, SampleInner> InnerDict { get; set; }
        public virtual IDictionary<string, string> Dict { get; set; }
    }

    [Trackable]
    public class SampleInner
    {
        public virtual string Description { get; set; }
    }

    [TestFixture]
    public class TrackablePocoEditModeTests
    {
        private SampleRoot NewRoot(out List<ChangeEventArgs> events)
        {
            var root = new SampleRoot().AsTrackable();
            var localEvents = new List<ChangeEventArgs>();
            ((ITrackable)root).Changed += (_, e) => localEvents.Add(e);
            events = localEvents;
            return root;
        }

        [Test]
        public void AsTrackable_Implements_ITrackable_And_IsDirty_Flows()
        {
            var root = new SampleRoot().AsTrackable();
            Assert.IsInstanceOf<ITrackable>(root);

            var t = (ITrackable)root;
            Assert.IsFalse(t.IsDirty, "Freshly created trackable should not be dirty.");

            root.Name = "John";
            Assert.IsTrue(t.IsDirty, "Any change should mark object dirty.");

            t.AcceptChanges();
            Assert.IsFalse(t.IsDirty, "AcceptChanges should reset IsDirty.");

            root.Age = 42;
            Assert.IsTrue(t.IsDirty, "After AcceptChanges, further changes should mark dirty again.");
        }

        [Test]
        public void SimpleProperty_Changes_Raise_PropertySet()
        {
            var root = NewRoot(out var events);

            root.Name = "John Doe";
            var e1 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e1.Kind);
            Assert.AreEqual("Name", e1.Path);
            Assert.IsNull(e1.OldValue);               // Old/New bubble from inner automatically by design
            Assert.AreEqual("John Doe", e1.NewValue);
            Assert.IsNull(e1.Index);

            events.Clear();
            root.Age = 30;
            var e2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e2.Kind);
            Assert.AreEqual("Age", e2.Path);
            Assert.AreEqual(30, e2.NewValue);
        }

        [Test]
        public void Setting_Same_Value_DoesNot_Raise_Change()
        {
            var root = NewRoot(out var events);

            root.Name = "X";
            events.Clear();

            root.Name = "X";
            Assert.IsEmpty(events, "Setting same value should not raise a change event.");
        }

        [Test]
        public void Inner_Assign_And_Child_Property_Change()
        {
            var root = NewRoot(out var events);
            
            root.Inner = new SampleInner { Description = "A" };
            var eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Inner", eSet.Path);
            Assert.IsNotNull(eSet.NewValue);

            events.Clear();
            root.Inner.Description = "B";
            var eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual("Inner.Description", eChild.Path);
            Assert.AreEqual("A", eChild.OldValue);
            Assert.AreEqual("B", eChild.NewValue);
            Assert.IsNull(eChild.Index);
        }

        [Test]
        public void Replacing_Inner_Detaches_Old_And_Attaches_New()
        {
            var root = NewRoot(out var events);
            
            root.Inner = new SampleInner { Description = "old" };
            events.Clear();
            
            var oldInner = root.Inner;
            root.Inner = new SampleInner { Description = "new" };
            var eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Inner", eSet.Path);

            // Changing the old inner should not bubble anymore
            events.Clear();
            oldInner.Description = "changed";
            Assert.IsEmpty(events, "Old inner should be detached after replacement.");

            // Changing the new inner should bubble
            events.Clear();
            root.Inner.Description = "changed2";
            var eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual("Inner.Description", eChild.Path);
        }

        [Test]
        public void IList_Of_Inner_Object_Collection_Operations()
        {
            var root = NewRoot(out var events);

            root.InnerList = new List<SampleInner>
            {
                new() { Description = "1" },
                new() { Description = "2" }
            };
            var eSetList = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.Kind);
            Assert.AreEqual("InnerList", eSetList.Path);

            // Add
            events.Clear();
            root.InnerList.Add(new SampleInner { Description = "3" });
            var eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("InnerList[2]", eAdd.Path);
            Assert.AreEqual(2, eAdd.Index);

            // Child change via index
            events.Clear();
            root.InnerList[0].Description = "1'";
            var eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Inner.Kind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.Inner.Inner.Kind);
            Assert.AreEqual("InnerList[0].Description", eChild.Path);
            Assert.AreEqual("1", eChild.OldValue);
            Assert.AreEqual("1'", eChild.NewValue);
            Assert.AreEqual(0, eChild.Index);

            // Replace at index
            events.Clear();
            root.InnerList[1] = new SampleInner { Description = "2'" };
            var eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("InnerList[1]", eRepl.Path);
            Assert.AreEqual(1, eRepl.Index);

            // RemoveAt
            events.Clear();
            root.InnerList.RemoveAt(0);
            var eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("InnerList[0]", eRem.Path);
            Assert.AreEqual(0, eRem.Index);

            // Clear
            events.Clear();
            root.InnerList.Clear();
            var eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("InnerList", eClr.Path);
            Assert.IsNull(eClr.Index);
        }

        [Test]
        public void IList_Of_Primitives_Collection_Operations()
        {
            var root = NewRoot(out var events);

            root.List = new List<string> { "Reading", "Traveling", "Cooking" };
            Assert.AreEqual(ChangeKind.PropertySet, events.Single().Kind);

            // Replace at index
            events.Clear();
            root.List[0] = "Hiking";
            var eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("List[0]", eRepl.Path);
            Assert.AreEqual(0, eRepl.Index);
            Assert.AreEqual("Reading", eRepl.OldValue);
            Assert.AreEqual("Hiking", eRepl.NewValue);

            // Add
            events.Clear();
            root.List.Add("Swimming");
            var eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("List[3]", eAdd.Path);
            Assert.AreEqual(3, eAdd.Index);
            Assert.AreEqual("Swimming", eAdd.NewValue);

            // Remove by value
            events.Clear();
            var removed = root.List.Remove("Traveling");
            Assert.IsTrue(removed);
            var eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("List[1]", eRem.Path);
            Assert.AreEqual("Traveling", eRem.OldValue);
            // Index may or may not be provided

            // Clear
            events.Clear();
            root.List.Clear();
            var eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("List", eClr.Path);
        }

        [Test]
        public void ISet_Of_Primitives_And_Objects_Operations()
        {
            var root = NewRoot(out var events);

            root.Set = new HashSet<string>();
            var eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Set", eSet.Path);

            // Add
            events.Clear();
            var added = root.Set.Add("alpha");
            Assert.IsTrue(added);
            var eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("Set[*]", eAdd.Path);
            Assert.AreEqual("alpha", eAdd.NewValue);
            Assert.IsNull(eAdd.Index);

            // Add duplicate - should not fire change
            events.Clear();
            added = root.Set.Add("alpha");
            Assert.IsFalse(added);
            Assert.IsEmpty(events, "Adding duplicate to set should not raise change.");

            // Remove
            events.Clear();
            var removed = root.Set.Remove("alpha");
            Assert.IsTrue(removed);
            var eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("Set[*]", eRem.Path);
            Assert.AreEqual("alpha", eRem.OldValue);
            Assert.IsNull(eRem.Index);

            // Set of objects with child change bubbling using wildcard path
            events.Clear();
            root.InnerSet = new HashSet<SampleInner>();
            var eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.Kind);
            Assert.AreEqual("InnerSet", eSet2.Path);

            events.Clear();
            var addedObj = root.InnerSet.Add(new SampleInner { Description = "X" });
            Assert.IsTrue(addedObj);
            var eAddObj = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAddObj.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAddObj.Inner.Kind);
            Assert.AreEqual("InnerSet[*]", eAddObj.Path);
            Assert.IsNull(eAddObj.Index);

            events.Clear();
            root.InnerSet.Single().Description = "Y";
            var eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Inner.Kind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.Inner.Inner.Kind);
            Assert.AreEqual("InnerSet[*].Description", eChild.Path);
            Assert.AreEqual("X", eChild.OldValue);
            Assert.AreEqual("Y", eChild.NewValue);
            Assert.IsNull(eChild.Index);

            // Clear
            events.Clear();
            root.InnerSet.Clear();
            var eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("InnerSet", eClr.Path);
        }

        [Test]
        public void IDictionary_Of_Primitives_And_Objects_Operations()
        {
            var root = NewRoot(out var events);

            // Dictionary of primitives
            root.Dict = new Dictionary<string, string>();
            var eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Dict", eSet.Path);

            // Add new key
            events.Clear();
            root.Dict["en"] = "Hello";
            var eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("Dict[en]", eAdd.Path);
            Assert.AreEqual("en", eAdd.Key);
            Assert.AreEqual("Hello", eAdd.NewValue);
            Assert.IsNull(eAdd.Index);

            // Replace existing key
            events.Clear();
            root.Dict["en"] = "Hello2";
            var eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("Dict[en]", eRepl.Path);
            Assert.AreEqual("en", eRepl.Key);
            Assert.AreEqual("Hello", eRepl.OldValue);
            Assert.AreEqual("Hello2", eRepl.NewValue);
            Assert.IsNull(eRepl.Index);

            // Remove key
            events.Clear();
            var removed = root.Dict.Remove("en");
            Assert.IsTrue(removed);
            var eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("Dict[en]", eRem.Path);
            Assert.AreEqual("en", eRem.Key);
            Assert.AreEqual("Hello2", eRem.OldValue);
            Assert.IsNull(eRem.Index);

            // Clear
            events.Clear();
            root.Dict.Clear();
            var eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("Dict", eClr.Path);

            // Dictionary of objects with child change bubbling using "InnerDict[key].Property"
            events.Clear();
            root.InnerDict = new Dictionary<string, SampleInner>();
            var eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.Kind);
            Assert.AreEqual("InnerDict", eSet2.Path);

            events.Clear();
            root.InnerDict["key"] = new SampleInner { Description = "A" };
            var eAdd2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd2.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd2.Inner.Kind);
            Assert.AreEqual("InnerDict[key]", eAdd2.Path);
            Assert.AreEqual("key", eAdd2.Key);

            events.Clear();
            root.InnerDict["key"].Description = "B";
            var eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Inner.Kind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.Inner.Inner.Kind);
            Assert.AreEqual("InnerDict[key].Description", eChild.Path);
            Assert.AreEqual("key", eChild.Key);
            Assert.AreEqual("A", eChild.OldValue);
            Assert.AreEqual("B", eChild.NewValue);

            // Replace value object and ensure old is detached
            events.Clear();
            var old = root.InnerDict["key"];
            root.InnerDict["key"] = new SampleInner { Description = "C" };
            var eRepl2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl2.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl2.Inner.Kind);
            Assert.AreEqual("InnerDict[key]", eRepl2.Path);
            Assert.AreEqual("key", eRepl2.Key);

            events.Clear();
            old.Description = "ZZZ";
            Assert.IsEmpty(events, "Old dictionary value should be detached after replacement.");

            events.Clear();
            root.InnerDict["key"].Description = "D";
            var eChild2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild2.Kind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild2.Inner.Kind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild2.Inner.Inner.Kind);
            Assert.AreEqual("InnerDict[key].Description", eChild2.Path);
            Assert.AreEqual("key", eChild2.Key);
            Assert.AreEqual("C", eChild2.OldValue);
            Assert.AreEqual("D", eChild2.NewValue);

            // Clear
            events.Clear();
            root.InnerDict.Clear();
            var eClr2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr2.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr2.Inner.Kind);
            Assert.AreEqual("InnerDict", eClr2.Path);
        }

        [Test]
        public void Replacing_Collections_Detaches_Old_Collections_And_Items()
        {
            var root = NewRoot(out var events);

            // IList detach
            root.InnerList = new List<SampleInner> { new() { Description = "L1" } };
            events.Clear();

            var oldList = root.InnerList;
            root.InnerList = new List<SampleInner>();
            var eSetList = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.Kind);
            Assert.AreEqual("InnerList", eSetList.Path);

            events.Clear();
            oldList[0].Description = "L1x";
            Assert.IsEmpty(events, "Changes in old list items should not bubble after the list is replaced.");

            // ISet detach
            root.InnerSet = new HashSet<SampleInner> { new() { Description = "S1" } };
            events.Clear();

            var oldSet = root.InnerSet;
            root.InnerSet = new HashSet<SampleInner>();
            var eSetSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetSet.Kind);
            Assert.AreEqual("InnerSet", eSetSet.Path);

            events.Clear();
            foreach (var it in oldSet) it.Description = "S1x";
            Assert.IsEmpty(events, "Changes in old set items should not bubble after the set is replaced.");

            // IDictionary detach
            root.InnerDict = new Dictionary<string, SampleInner> { ["k"] = new() { Description = "D1" } };
            events.Clear();

            var oldDict = root.InnerDict;
            root.InnerDict = new Dictionary<string, SampleInner>();
            var eSetDict = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetDict.Kind);
            Assert.AreEqual("InnerDict", eSetDict.Path);

            events.Clear();
            oldDict["k"].Description = "D1x";
            Assert.IsEmpty(events, "Changes in old dictionary items should not bubble after the dictionary is replaced.");
        }

        [Test]
        public void AcceptChanges_Resets_Dirty_And_Does_Not_Suppress_Events()
        {
            var root = NewRoot(out var events);
            var t = (ITrackable)root;

            root.Name = "A";
            Assert.IsTrue(t.IsDirty);
            Assert.IsNotEmpty(events);

            events.Clear();
            t.AcceptChanges();
            Assert.IsFalse(t.IsDirty);

            // Next change still fires event and marks dirty
            events.Clear();
            root.Name = "B";
            Assert.IsTrue(t.IsDirty);
            var e = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e.Kind);
            Assert.AreEqual("Name", e.Path);
            Assert.AreEqual("A", e.OldValue);
            Assert.AreEqual("B", e.NewValue);
        }
    }
}