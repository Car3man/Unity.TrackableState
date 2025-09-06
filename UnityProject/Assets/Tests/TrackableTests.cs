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
            TrackableSampleRoot root = new SampleRoot().AsTrackable();
            List<ChangeEventArgs> localEvents = new List<ChangeEventArgs>();
            ((ITrackable)root).Changed += (_, e) => localEvents.Add(e);
            events = localEvents;
            return root;
        }

        [Test]
        public void AsTrackable_Implements_ITrackable_And_IsDirty_Flows()
        {
            TrackableSampleRoot root = new SampleRoot().AsTrackable();
            Assert.IsInstanceOf<ITrackable>(root);

            ITrackable t = root;
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
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            root.Name = "John Doe";
            ChangeEventArgs e1 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e1.Kind);
            Assert.AreEqual("Name", e1.Path);
            Assert.IsNull(e1.OldValue);               // Old/New bubble from inner automatically by design
            Assert.AreEqual("John Doe", e1.NewValue);
            Assert.IsNull(e1.Index);

            events.Clear();
            root.Age = 30;
            ChangeEventArgs e2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e2.Kind);
            Assert.AreEqual("Age", e2.Path);
            Assert.AreEqual(30, e2.NewValue);
        }

        [Test]
        public void Setting_Same_Value_DoesNot_Raise_Change()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            root.Name = "X";
            events.Clear();

            root.Name = "X";
            Assert.IsEmpty(events, "Setting same value should not raise a change event.");
        }

        [Test]
        public void Inner_Assign_And_Child_Property_Change()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
            
            root.Inner = new SampleInner { Description = "A" };
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Inner", eSet.Path);
            Assert.IsNotNull(eSet.NewValue);

            events.Clear();
            root.Inner.Description = "B";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual("Inner.Description", eChild.Path);
            Assert.AreEqual("A", eChild.OldValue);
            Assert.AreEqual("B", eChild.NewValue);
            Assert.IsNull(eChild.Index);
        }

        [Test]
        public void Replacing_Inner_Detaches_Old_And_Attaches_New()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
            
            root.Inner = new SampleInner { Description = "old" };
            events.Clear();
            
            SampleInner oldInner = root.Inner;
            root.Inner = new SampleInner { Description = "new" };
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Inner", eSet.Path);

            // Changing the old inner should not bubble anymore
            events.Clear();
            oldInner.Description = "changed";
            Assert.IsEmpty(events, "Old inner should be detached after replacement.");

            // Changing the new inner should bubble
            events.Clear();
            root.Inner.Description = "changed2";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual("Inner.Description", eChild.Path);
        }

        [Test]
        public void IList_Of_Inner_Object_Collection_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            root.InnerList = new List<SampleInner>
            {
                new() { Description = "1" },
                new() { Description = "2" }
            };
            ChangeEventArgs eSetList = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.Kind);
            Assert.AreEqual("InnerList", eSetList.Path);

            // Add
            events.Clear();
            root.InnerList.Add(new SampleInner { Description = "3" });
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("InnerList[2]", eAdd.Path);
            Assert.AreEqual(2, eAdd.Index);

            // Child change via index
            events.Clear();
            root.InnerList[0].Description = "1'";
            ChangeEventArgs eChild = events.Single();
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
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("InnerList[1]", eRepl.Path);
            Assert.AreEqual(1, eRepl.Index);

            // RemoveAt
            events.Clear();
            root.InnerList.RemoveAt(0);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("InnerList[0]", eRem.Path);
            Assert.AreEqual(0, eRem.Index);

            // Clear
            events.Clear();
            root.InnerList.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("InnerList", eClr.Path);
            Assert.IsNull(eClr.Index);
        }

        [Test]
        public void IList_Of_Primitives_Collection_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            root.List = new List<string> { "Reading", "Traveling", "Cooking" };
            Assert.AreEqual(ChangeKind.PropertySet, events.Single().Kind);

            // Replace at index
            events.Clear();
            root.List[0] = "Hiking";
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("List[0]", eRepl.Path);
            Assert.AreEqual(0, eRepl.Index);
            Assert.AreEqual("Reading", eRepl.OldValue);
            Assert.AreEqual("Hiking", eRepl.NewValue);

            // Add
            events.Clear();
            root.List.Add("Swimming");
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("List[3]", eAdd.Path);
            Assert.AreEqual(3, eAdd.Index);
            Assert.AreEqual("Swimming", eAdd.NewValue);

            // Remove by value
            events.Clear();
            bool removed = root.List.Remove("Traveling");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("List[1]", eRem.Path);
            Assert.AreEqual("Traveling", eRem.OldValue);
            // Index may or may not be provided

            // Clear
            events.Clear();
            root.List.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("List", eClr.Path);
        }

        [Test]
        public void ISet_Of_Primitives_And_Objects_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            root.Set = new HashSet<string>();
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Set", eSet.Path);

            // Add
            events.Clear();
            bool added = root.Set.Add("alpha");
            Assert.IsTrue(added);
            ChangeEventArgs eAdd = events.Single();
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
            bool removed = root.Set.Remove("alpha");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("Set[*]", eRem.Path);
            Assert.AreEqual("alpha", eRem.OldValue);
            Assert.IsNull(eRem.Index);

            // Set of objects with child change bubbling using wildcard path
            events.Clear();
            root.InnerSet = new HashSet<SampleInner>();
            ChangeEventArgs eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.Kind);
            Assert.AreEqual("InnerSet", eSet2.Path);

            events.Clear();
            bool addedObj = root.InnerSet.Add(new SampleInner { Description = "X" });
            Assert.IsTrue(addedObj);
            ChangeEventArgs eAddObj = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAddObj.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAddObj.Inner.Kind);
            Assert.AreEqual("InnerSet[*]", eAddObj.Path);
            Assert.IsNull(eAddObj.Index);

            events.Clear();
            root.InnerSet.Single().Description = "Y";
            ChangeEventArgs eChild = events.Single();
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
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("InnerSet", eClr.Path);
        }

        [Test]
        public void IDictionary_Of_Primitives_And_Objects_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            // Dictionary of primitives
            root.Dict = new Dictionary<string, string>();
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.Kind);
            Assert.AreEqual("Dict", eSet.Path);

            // Add new key
            events.Clear();
            root.Dict["en"] = "Hello";
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.Inner.Kind);
            Assert.AreEqual("Dict[en]", eAdd.Path);
            Assert.AreEqual("en", eAdd.Key);
            Assert.AreEqual("Hello", eAdd.NewValue);
            Assert.IsNull(eAdd.Index);

            // Replace existing key
            events.Clear();
            root.Dict["en"] = "Hello2";
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.Inner.Kind);
            Assert.AreEqual("Dict[en]", eRepl.Path);
            Assert.AreEqual("en", eRepl.Key);
            Assert.AreEqual("Hello", eRepl.OldValue);
            Assert.AreEqual("Hello2", eRepl.NewValue);
            Assert.IsNull(eRepl.Index);

            // Remove key
            events.Clear();
            bool removed = root.Dict.Remove("en");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.Kind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.Inner.Kind);
            Assert.AreEqual("Dict[en]", eRem.Path);
            Assert.AreEqual("en", eRem.Key);
            Assert.AreEqual("Hello2", eRem.OldValue);
            Assert.IsNull(eRem.Index);

            // Clear
            events.Clear();
            root.Dict.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.Inner.Kind);
            Assert.AreEqual("Dict", eClr.Path);

            // Dictionary of objects with child change bubbling using "InnerDict[key].Property"
            events.Clear();
            root.InnerDict = new Dictionary<string, SampleInner>();
            ChangeEventArgs eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.Kind);
            Assert.AreEqual("InnerDict", eSet2.Path);

            events.Clear();
            root.InnerDict["key"] = new SampleInner { Description = "A" };
            ChangeEventArgs eAdd2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd2.Kind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd2.Inner.Kind);
            Assert.AreEqual("InnerDict[key]", eAdd2.Path);
            Assert.AreEqual("key", eAdd2.Key);

            events.Clear();
            root.InnerDict["key"].Description = "B";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Kind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.Inner.Kind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.Inner.Inner.Kind);
            Assert.AreEqual("InnerDict[key].Description", eChild.Path);
            Assert.AreEqual("key", eChild.Key);
            Assert.AreEqual("A", eChild.OldValue);
            Assert.AreEqual("B", eChild.NewValue);

            // Replace value object and ensure old is detached
            events.Clear();
            SampleInner old = root.InnerDict["key"];
            root.InnerDict["key"] = new SampleInner { Description = "C" };
            ChangeEventArgs eRepl2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl2.Kind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl2.Inner.Kind);
            Assert.AreEqual("InnerDict[key]", eRepl2.Path);
            Assert.AreEqual("key", eRepl2.Key);

            events.Clear();
            old.Description = "ZZZ";
            Assert.IsEmpty(events, "Old dictionary value should be detached after replacement.");

            events.Clear();
            root.InnerDict["key"].Description = "D";
            ChangeEventArgs eChild2 = events.Single();
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
            ChangeEventArgs eClr2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr2.Kind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr2.Inner.Kind);
            Assert.AreEqual("InnerDict", eClr2.Path);
        }

        [Test]
        public void Replacing_Collections_Detaches_Old_Collections_And_Items()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);

            // IList detach
            root.InnerList = new List<SampleInner> { new() { Description = "L1" } };
            events.Clear();

            IList<SampleInner> oldList = root.InnerList;
            root.InnerList = new List<SampleInner>();
            ChangeEventArgs eSetList = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.Kind);
            Assert.AreEqual("InnerList", eSetList.Path);

            events.Clear();
            oldList[0].Description = "L1x";
            Assert.IsEmpty(events, "Changes in old list items should not bubble after the list is replaced.");

            // ISet detach
            root.InnerSet = new HashSet<SampleInner> { new() { Description = "S1" } };
            events.Clear();

            ISet<SampleInner> oldSet = root.InnerSet;
            root.InnerSet = new HashSet<SampleInner>();
            ChangeEventArgs eSetSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetSet.Kind);
            Assert.AreEqual("InnerSet", eSetSet.Path);

            events.Clear();
            foreach (SampleInner it in oldSet) it.Description = "S1x";
            Assert.IsEmpty(events, "Changes in old set items should not bubble after the set is replaced.");

            // IDictionary detach
            root.InnerDict = new Dictionary<string, SampleInner> { ["k"] = new() { Description = "D1" } };
            events.Clear();

            IDictionary<string, SampleInner> oldDict = root.InnerDict;
            root.InnerDict = new Dictionary<string, SampleInner>();
            ChangeEventArgs eSetDict = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetDict.Kind);
            Assert.AreEqual("InnerDict", eSetDict.Path);

            events.Clear();
            oldDict["k"].Description = "D1x";
            Assert.IsEmpty(events, "Changes in old dictionary items should not bubble after the dictionary is replaced.");
        }

        [Test]
        public void AcceptChanges_Resets_Dirty_And_Does_Not_Suppress_Events()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
            ITrackable t = (ITrackable)root;

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
            ChangeEventArgs e = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e.Kind);
            Assert.AreEqual("Name", e.Path);
            Assert.AreEqual("A", e.OldValue);
            Assert.AreEqual("B", e.NewValue);
        }
        
        private TrackableSampleRoot CreateTrackableSample()
        {
            SampleRoot source = new SampleRoot
            {
                Name = "Initial",
                Age = 30,
                Inner = new SampleInner { Description = "Inner-Init" },
                InnerList = new List<SampleInner>
                {
                    new() { Description = "IL-Init-1" },
                    new() { Description = "IL-Init-2" },
                },
                List = new List<string> { "L-1", "L-2" },
                InnerSet = new HashSet<SampleInner>
                {
                    new() { Description = "IS-Init-1" },
                    new() { Description = "IS-Init-2" },
                },
                Set = new HashSet<string> { "S-1", "S-2" },
                InnerDict = new Dictionary<string, SampleInner>
                {
                    ["K1"] = new() { Description = "ID-Init-1" },
                    ["K2"] = new() { Description = "ID-Init-2" },
                },
                Dict = new Dictionary<string, string>
                {
                    ["K1"] = "V-Init-1",
                    ["K2"] = "V-Init-2",
                }
            };

            return source.AsTrackable();
        }
        
        [Test]
        public void AsNormal_ReturnsPlainPocoTypes_NotTrackable()
        {
            TrackableSampleRoot trackable = CreateTrackableSample();
            SampleRoot normal = trackable.AsNormal();
            
            Assert.IsNotNull(normal);
            Assert.IsNotInstanceOf<TrackableSampleRoot>(normal, "AsNormal should return plain SampleRoot, not TrackableSampleRoot.");
            Assert.IsFalse(normal is ITrackable, "AsNormal should not return an ITrackable instance.");
            
            if (normal.Inner != null)
            {
                Assert.IsFalse(normal.Inner is ITrackable, "Inner must be a plain SampleInner.");
            }
            
            Assert.IsFalse(normal.InnerList is ITrackable, "InnerList must be a plain IList<T>.");
            Assert.IsFalse(normal.List is ITrackable, "List must be a plain IList<T>.");
            Assert.IsFalse(normal.InnerSet is ITrackable, "InnerSet must be a plain ISet<T>.");
            Assert.IsFalse(normal.Set is ITrackable, "Set must be a plain ISet<T>.");
            Assert.IsFalse(normal.InnerDict is ITrackable, "InnerDict must be a plain IDictionary<TKey, TValue>.");
            Assert.IsFalse(normal.Dict is ITrackable, "Dict must be a plain IDictionary<TKey, TValue>.");
            
            if (normal.InnerList != null)
            {
                foreach (SampleInner item in normal.InnerList)
                {
                    Assert.IsFalse(item is ITrackable, "InnerList elements must be plain SampleInner.");
                }
            }

            if (normal.InnerSet != null)
            {
                foreach (SampleInner item in normal.InnerSet)
                {
                    Assert.IsFalse(item is ITrackable, "InnerSet elements must be plain SampleInner.");
                }
            }

            if (normal.InnerDict != null)
            {
                foreach (KeyValuePair<string, SampleInner> kv in normal.InnerDict)
                {
                    Assert.IsFalse(kv.Value is ITrackable, "InnerDict values must be plain SampleInner.");
                }
            }
        }

        [Test]
        public void AsNormal_ValuesMatchLatestTrackableState()
        {
            TrackableSampleRoot trackable = CreateTrackableSample();
            trackable.Name = "John";
            trackable.Age = 42;
            trackable.Inner = new SampleInner { Description = "Inner-Updated" };
            trackable.InnerList = new List<SampleInner>
            {
                new() { Description = "IL-1" },
                new() { Description = "IL-2" },
            };
            trackable.List = new List<string> { "A", "B", "C" };
            trackable.InnerSet = new HashSet<SampleInner>
            {
                new() { Description = "IS-1" },
                new() { Description = "IS-2" },
            };
            trackable.Set = new HashSet<string> { "X", "Y" };
            trackable.InnerDict = new Dictionary<string, SampleInner>
            {
                ["K1"] = new() { Description = "ID-1" },
                ["K2"] = new() { Description = "ID-2" },
            };
            trackable.Dict = new Dictionary<string, string>
            {
                ["K1"] = "V1",
                ["K2"] = "V2",
            };
            
            SampleRoot normal = trackable.AsNormal();
            
            Assert.AreEqual(trackable.Name, normal.Name);
            Assert.AreEqual(trackable.Age, normal.Age);
            Assert.AreEqual(trackable.Inner?.Description, normal.Inner?.Description);
            
            CollectionAssert.AreEqual(
                trackable.InnerList?.Select(x => x?.Description).ToList(),
                normal.InnerList?.Select(x => x?.Description).ToList(),
                "InnerList contents should match by Description");

            CollectionAssert.AreEqual(
                trackable.List?.ToList(),
                normal.List?.ToList(),
                "List contents should match");
            
            CollectionAssert.AreEquivalent(
                trackable.InnerSet?.Select(x => x?.Description).ToList(),
                normal.InnerSet?.Select(x => x?.Description).ToList(),
                "InnerSet contents should match by Description");

            CollectionAssert.AreEquivalent(
                trackable.Set?.ToList(),
                normal.Set?.ToList(),
                "Set contents should match");
            
            CollectionAssert.AreEquivalent(
                trackable.InnerDict?.Keys.ToList(),
                normal.InnerDict?.Keys.ToList(),
                "InnerDict keys should match");
            if (trackable.InnerDict != null && normal.InnerDict != null)
            {
                foreach (string k in trackable.InnerDict.Keys)
                {
                    Assert.AreEqual(trackable.InnerDict[k]?.Description, normal.InnerDict[k]?.Description, $"InnerDict value mismatch for key {k}");
                }
            }

            CollectionAssert.AreEquivalent(
                trackable.Dict?.Keys.ToList(),
                normal.Dict?.Keys.ToList(),
                "Dict keys should match");
            if (trackable.Dict != null && normal.Dict != null)
            {
                foreach (string k in trackable.Dict.Keys)
                {
                    Assert.AreEqual(trackable.Dict[k], normal.Dict[k], $"Dict value mismatch for key {k}");
                }
            }
        }

        [Test]
        public void AsNormal_ResultIsDetached_MutationsDoNotAffectTrackable()
        {
            TrackableSampleRoot trackable = CreateTrackableSample();
            
            var snapshot = new
            {
                trackable.Name,
                trackable.Age,
                InnerDesc = trackable.Inner?.Description,
                InnerList = trackable.InnerList?.Select(x => x?.Description).ToList(),
                List = trackable.List?.ToList(),
                InnerSet = trackable.InnerSet?.Select(x => x?.Description).ToHashSet(),
                Set = trackable.Set?.ToHashSet(),
                InnerDict = trackable.InnerDict?.ToDictionary(kv => kv.Key, kv => kv.Value?.Description),
                Dict = trackable.Dict?.ToDictionary(kv => kv.Key, kv => kv.Value),
            };

            SampleRoot normal = trackable.AsNormal();
            
            normal.Name = "Changed-Name";
            normal.Age += 10;

            if (normal.Inner != null)
            {
                normal.Inner.Description = "Changed-Inner";
            }

            if (normal.InnerList is { Count: > 0 })
            {
                normal.InnerList[0] = new SampleInner { Description = "Changed-IL-0" };
                normal.InnerList.Add(new SampleInner { Description = "Added-IL" });
            }

            if (normal.List != null)
            {
                normal.List.Add("Added-L");
                if (normal.List.Count > 0) normal.List[0] = "Changed-L-0";
            }

            if (normal.InnerSet != null)
            {
                normal.InnerSet.Add(new SampleInner { Description = "Added-IS" });
            }

            if (normal.Set != null)
            {
                normal.Set.Add("Added-S");
            }

            if (normal.InnerDict != null)
            {
                normal.InnerDict["K1"] = new SampleInner { Description = "Changed-ID-1" };
                normal.InnerDict["K-New"] = new SampleInner { Description = "Added-ID" };
            }

            if (normal.Dict != null)
            {
                normal.Dict["K1"] = "Changed-V1";
                normal.Dict["K-New"] = "Added-V";
            }
            
            Assert.AreEqual(snapshot.Name, trackable.Name, "Trackable.Name should not be affected by normal mutations.");
            Assert.AreEqual(snapshot.Age, trackable.Age, "Trackable.Age should not be affected by normal mutations.");
            Assert.AreEqual(snapshot.InnerDesc, trackable.Inner?.Description, "Trackable.Inner should not be affected by normal mutations.");

            CollectionAssert.AreEqual(snapshot.InnerList, trackable.InnerList?.Select(x => x?.Description).ToList(), "Trackable.InnerList should not be affected.");
            CollectionAssert.AreEqual(snapshot.List, trackable.List?.ToList(), "Trackable.List should not be affected.");
            CollectionAssert.AreEquivalent(snapshot.InnerSet, trackable.InnerSet?.Select(x => x?.Description).ToHashSet(), "Trackable.InnerSet should not be affected.");
            CollectionAssert.AreEquivalent(snapshot.Set, trackable.Set?.ToHashSet(), "Trackable.Set should not be affected.");

            if (snapshot.InnerDict == null)
            {
                Assert.IsNull(trackable.InnerDict, "Trackable.InnerDict should remain null.");
            }
            else
            {
                CollectionAssert.AreEquivalent(snapshot.InnerDict.Keys, trackable.InnerDict.Keys, "Trackable.InnerDict keys should not change.");
                foreach (string k in snapshot.InnerDict.Keys)
                {
                    Assert.AreEqual(snapshot.InnerDict[k], trackable.InnerDict[k]?.Description, $"Trackable.InnerDict value changed for key {k}");
                }
            }

            if (snapshot.Dict == null)
            {
                Assert.IsNull(trackable.Dict, "Trackable.Dict should remain null.");
            }
            else
            {
                CollectionAssert.AreEquivalent(snapshot.Dict.Keys, trackable.Dict.Keys, "Trackable.Dict keys should not change.");
                foreach (string k in snapshot.Dict.Keys)
                {
                    Assert.AreEqual(snapshot.Dict[k], trackable.Dict[k], $"Trackable.Dict value changed for key {k}");
                }
            }
        }
    }
}