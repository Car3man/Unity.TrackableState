using Klopoff.TrackableState.Core;
using NUnit.Framework;
using TrackableState.Packer;

namespace TrackableState.Tests
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
    public class TrackableStateTests
    {
        private SampleRoot NewRoot(out List<ChangeEventArgs> events)
        {
            List<ChangeEventArgs> localEvents = new List<ChangeEventArgs>();
            TrackableSampleRoot root = new SampleRoot().AsTrackable();
            root.Changed += OnChange;
            events = localEvents;
            return root;
            void OnChange(object _, in ChangeEventArgs e) => localEvents.Add(e);
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
            Assert.AreEqual(ChangeKind.PropertySet, e1.path[0].changeKind);
            Assert.AreEqual("Name", e1.PathString);
            Assert.AreEqual(null, e1.oldValue.Get<string>());
            Assert.AreEqual("John Doe", e1.newValue.Get<string>());
            Assert.AreEqual(-1, e1.index);

            events.Clear();
            root.Age = 30;
            ChangeEventArgs e2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e2.path[0].changeKind);
            Assert.AreEqual("Age", e2.PathString);
            Assert.AreEqual(30, e2.newValue.Get<int>());
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
            Assert.AreEqual(ChangeKind.PropertySet, eSet.path[0].changeKind);
            Assert.AreEqual("Inner", eSet.PathString);
            Assert.IsNotNull(eSet.newValue.Get<SampleInner>());
        
            events.Clear();
            root.Inner.Description = "B";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[0].changeKind);
            Assert.AreEqual("Inner.Description", eChild.PathString);
            Assert.AreEqual("A", eChild.oldValue.Get<string>());
            Assert.AreEqual("B", eChild.newValue.Get<string>());
            Assert.AreEqual(-1, eChild.index);
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
            Assert.AreEqual(ChangeKind.PropertySet, eSet.path[0].changeKind);
            Assert.AreEqual("Inner", eSet.PathString);
        
            events.Clear();
            oldInner.Description = "changed";
            Assert.IsEmpty(events, "Old inner should be detached after replacement.");
        
            events.Clear();
            root.Inner.Description = "changed2";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[0].changeKind);
            Assert.AreEqual("Inner.Description", eChild.PathString);
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
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.path[0].changeKind);
            Assert.AreEqual("InnerList", eSetList.PathString);
        
            events.Clear();
            root.InnerList.Add(new SampleInner { Description = "3" });
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.path[1].changeKind);
            Assert.AreEqual("InnerList[2]", eAdd.PathString);
            Assert.AreEqual(2, eAdd.index);
        
            events.Clear();
            root.InnerList[0].Description = "1'";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[0].changeKind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[1].changeKind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.path[2].changeKind);
            Assert.AreEqual("InnerList[0].Description", eChild.PathString);
            Assert.AreEqual("1", eChild.oldValue.Get<string>());
            Assert.AreEqual("1'", eChild.newValue.Get<string>());
            Assert.AreEqual(0, eChild.index);
        
            events.Clear();
            root.InnerList[1] = new SampleInner { Description = "2'" };
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.path[1].changeKind);
            Assert.AreEqual("InnerList[1]", eRepl.PathString);
            Assert.AreEqual(1, eRepl.index);
        
            events.Clear();
            root.InnerList.RemoveAt(0);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.path[1].changeKind);
            Assert.AreEqual("InnerList[0]", eRem.PathString);
            Assert.AreEqual(0, eRem.index);
        
            events.Clear();
            root.InnerList.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.path[1].changeKind);
            Assert.AreEqual("InnerList", eClr.PathString);
            Assert.AreEqual(-1, eClr.index);
        }
        
        [Test]
        public void IList_Of_Primitives_Collection_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
        
            root.List = new List<string> { "Reading", "Traveling", "Cooking" };
            Assert.AreEqual(ChangeKind.PropertySet, events.Single().path[0].changeKind);
        
            events.Clear();
            root.List[0] = "Hiking";
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.path[1].changeKind);
            Assert.AreEqual("List[0]", eRepl.PathString);
            Assert.AreEqual(0, eRepl.index);
            Assert.AreEqual("Reading", eRepl.oldValue.Get<string>());
            Assert.AreEqual("Hiking", eRepl.newValue.Get<string>());
        
            events.Clear();
            root.List.Add("Swimming");
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.path[1].changeKind);
            Assert.AreEqual("List[3]", eAdd.PathString);
            Assert.AreEqual(3, eAdd.index);
            Assert.AreEqual("Swimming", eAdd.newValue.Get<string>());
        
            events.Clear();
            bool removed = root.List.Remove("Traveling");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.path[1].changeKind);
            Assert.AreEqual("List[1]", eRem.PathString);
            Assert.AreEqual("Traveling", eRem.oldValue.Get<string>());
        
            events.Clear();
            root.List.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.path[1].changeKind);
            Assert.AreEqual("List", eClr.PathString);
        }
        
        [Test]
        public void ISet_Of_Primitives_And_Objects_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
        
            root.Set = new HashSet<string>();
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.path[0].changeKind);
            Assert.AreEqual("Set", eSet.PathString);
        
            events.Clear();
            bool added = root.Set.Add("alpha");
            Assert.IsTrue(added);
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.path[1].changeKind);
            Assert.AreEqual("Set[*]", eAdd.PathString);
            Assert.AreEqual("alpha", eAdd.newValue.Get<string>());
            Assert.AreEqual(-1, eAdd.index);
        
            events.Clear();
            added = root.Set.Add("alpha");
            Assert.IsFalse(added);
            Assert.IsEmpty(events, "Adding duplicate to set should not raise change.");
        
            events.Clear();
            bool removed = root.Set.Remove("alpha");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.path[1].changeKind);
            Assert.AreEqual("Set[*]", eRem.PathString);
            Assert.AreEqual("alpha", eRem.oldValue.Get<string>());
            Assert.AreEqual(-1, eRem.index);
        
            events.Clear();
            root.InnerSet = new HashSet<SampleInner>();
            ChangeEventArgs eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.path[0].changeKind);
            Assert.AreEqual("InnerSet", eSet2.PathString);
        
            events.Clear();
            bool addedObj = root.InnerSet.Add(new SampleInner { Description = "X" });
            Assert.IsTrue(addedObj);
            ChangeEventArgs eAddObj = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAddObj.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAddObj.path[1].changeKind);
            Assert.AreEqual("InnerSet[*]", eAddObj.PathString);
            Assert.AreEqual(-1, eAddObj.index);
        
            events.Clear();
            root.InnerSet.Single().Description = "Y";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[0].changeKind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[1].changeKind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.path[2].changeKind);
            Assert.AreEqual("InnerSet[*].Description", eChild.PathString);
            Assert.AreEqual("X", eChild.oldValue.Get<string>());
            Assert.AreEqual("Y", eChild.newValue.Get<string>());
            Assert.AreEqual(-1, eChild.index);
        
            events.Clear();
            root.InnerSet.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.path[1].changeKind);
            Assert.AreEqual("InnerSet", eClr.PathString);
        }
        
        [Test]
        public void IDictionary_Of_Primitives_And_Objects_Operations()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
        
            root.Dict = new Dictionary<string, string>();
            ChangeEventArgs eSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet.path[0].changeKind);
            Assert.AreEqual("Dict", eSet.PathString);
        
            events.Clear();
            root.Dict["en"] = "Hello";
            ChangeEventArgs eAdd = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd.path[1].changeKind);
            Assert.AreEqual("Dict[en]", eAdd.PathString);
            Assert.AreEqual("en", eAdd.key.Get<string>());
            Assert.AreEqual("Hello", eAdd.newValue.Get<string>());
            Assert.AreEqual(-1, eAdd.index);
        
            events.Clear();
            root.Dict["en"] = "Hello2";
            ChangeEventArgs eRepl = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl.path[1].changeKind);
            Assert.AreEqual("Dict[en]", eRepl.PathString);
            Assert.AreEqual("en", eRepl.key.Get<string>());
            Assert.AreEqual("Hello", eRepl.oldValue.Get<string>());
            Assert.AreEqual("Hello2", eRepl.newValue.Get<string>());
            Assert.AreEqual(-1, eRepl.index);
        
            events.Clear();
            bool removed = root.Dict.Remove("en");
            Assert.IsTrue(removed);
            ChangeEventArgs eRem = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRem.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionRemove, eRem.path[1].changeKind);
            Assert.AreEqual("Dict[en]", eRem.PathString);
            Assert.AreEqual("en", eRem.key.Get<string>());
            Assert.AreEqual("Hello2", eRem.oldValue.Get<string>());
            Assert.AreEqual(-1, eRem.index);
        
            events.Clear();
            root.Dict.Clear();
            ChangeEventArgs eClr = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr.path[1].changeKind);
            Assert.AreEqual("Dict", eClr.PathString);
        
            events.Clear();
            root.InnerDict = new Dictionary<string, SampleInner>();
            ChangeEventArgs eSet2 = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSet2.path[0].changeKind);
            Assert.AreEqual("InnerDict", eSet2.PathString);
        
            events.Clear();
            root.InnerDict["key"] = new SampleInner { Description = "A" };
            ChangeEventArgs eAdd2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eAdd2.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionAdd, eAdd2.path[1].changeKind);
            Assert.AreEqual("InnerDict[key]", eAdd2.PathString);
            Assert.AreEqual("key", eAdd2.key.Get<string>());
        
            events.Clear();
            root.InnerDict["key"].Description = "B";
            ChangeEventArgs eChild = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[0].changeKind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild.path[1].changeKind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild.path[2].changeKind);
            Assert.AreEqual("InnerDict[key].Description", eChild.PathString);
            Assert.AreEqual("key", eChild.key.Get<string>());
            Assert.AreEqual("A", eChild.oldValue.Get<string>());
            Assert.AreEqual("B", eChild.newValue.Get<string>());
        
            events.Clear();
            SampleInner old = root.InnerDict["key"];
            root.InnerDict["key"] = new SampleInner { Description = "C" };
            ChangeEventArgs eRepl2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eRepl2.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionReplace, eRepl2.path[1].changeKind);
            Assert.AreEqual("InnerDict[key]", eRepl2.PathString);
            Assert.AreEqual("key", eRepl2.key.Get<string>());
        
            events.Clear();
            old.Description = "ZZZ";
            Assert.IsEmpty(events, "Old dictionary value should be detached after replacement.");
        
            events.Clear();
            root.InnerDict["key"].Description = "D";
            ChangeEventArgs eChild2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eChild2.path[0].changeKind);
            Assert.AreEqual(ChangeKind.ChildChange, eChild2.path[1].changeKind);
            Assert.AreEqual(ChangeKind.PropertySet, eChild2.path[2].changeKind);
            Assert.AreEqual("InnerDict[key].Description", eChild2.PathString);
            Assert.AreEqual("key", eChild2.key.Get<string>());
            Assert.AreEqual("C", eChild2.oldValue.Get<string>());
            Assert.AreEqual("D", eChild2.newValue.Get<string>());
        
            events.Clear();
            root.InnerDict.Clear();
            ChangeEventArgs eClr2 = events.Single();
            Assert.AreEqual(ChangeKind.ChildChange, eClr2.path[0].changeKind);
            Assert.AreEqual(ChangeKind.CollectionClear, eClr2.path[1].changeKind);
            Assert.AreEqual("InnerDict", eClr2.PathString);
        }
        
        [Test]
        public void Replacing_Collections_Detaches_Old_Collections_And_Items()
        {
            SampleRoot root = NewRoot(out List<ChangeEventArgs> events);
        
            root.InnerList = new List<SampleInner> { new() { Description = "L1" } };
            events.Clear();
        
            IList<SampleInner> oldList = root.InnerList;
            root.InnerList = new List<SampleInner>();
            ChangeEventArgs eSetList = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetList.path[0].changeKind);
            Assert.AreEqual("InnerList", eSetList.PathString);
        
            events.Clear();
            oldList[0].Description = "L1x";
            Assert.IsEmpty(events, "Changes in old list items should not bubble after the list is replaced.");
        
            root.InnerSet = new HashSet<SampleInner> { new() { Description = "S1" } };
            events.Clear();
        
            ISet<SampleInner> oldSet = root.InnerSet;
            root.InnerSet = new HashSet<SampleInner>();
            ChangeEventArgs eSetSet = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetSet.path[0].changeKind);
            Assert.AreEqual("InnerSet", eSetSet.PathString);
        
            events.Clear();
            foreach (SampleInner it in oldSet) it.Description = "S1x";
            Assert.IsEmpty(events, "Changes in old set items should not bubble after the set is replaced.");
        
            root.InnerDict = new Dictionary<string, SampleInner> { ["k"] = new() { Description = "D1" } };
            events.Clear();
        
            IDictionary<string, SampleInner> oldDict = root.InnerDict;
            root.InnerDict = new Dictionary<string, SampleInner>();
            ChangeEventArgs eSetDict = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, eSetDict.path[0].changeKind);
            Assert.AreEqual("InnerDict", eSetDict.PathString);
        
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
        
            events.Clear();
            root.Name = "B";
            Assert.IsTrue(t.IsDirty);
            ChangeEventArgs e = events.Single();
            Assert.AreEqual(ChangeKind.PropertySet, e.path[0].changeKind);
            Assert.AreEqual("Name", e.PathString);
            Assert.AreEqual("A", e.oldValue.Get<string>());
            Assert.AreEqual("B", e.newValue.Get<string>());
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
        
        private (SampleRoot root, ChangeLogBuffer buffer) NewRootWithBuffer(SampleRoot plain)
        {
            ChangeLogBuffer buffer = new ChangeLogBuffer();
            TrackableSampleRoot root = plain.AsTrackable();
            root.Changed += OnChange;
            return (root, buffer);
            void OnChange(object _, in ChangeEventArgs e) => buffer.Add(e);
        }
        
        [Test]
        public void Pack_Merges_Sequential_Property_Changes_For_Same_Path()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithBuffer(new SampleRoot
            {
                Name = "0"
            });

            root.Name = "A";
            root.Name = "B";
            root.Name = "C";

            Assert.AreEqual(3, buffer.Snapshot.Count, "Precondition: expected three raw change events before packing.");
            
            buffer.Pack();
            
            IReadOnlyList<ChangeEventArgs> packed = buffer.Snapshot;
            Assert.AreEqual(1, packed.Count, "All Name changes without conflicts should merge into one event.");

            ChangeEventArgs e = packed[0];
            Assert.AreEqual("Name", e.PathString);
            Assert.AreEqual("0", e.oldValue.Get<string>(), "Merged oldValue must come from the initial state.");
            Assert.AreEqual("C", e.newValue.Get<string>(), "Merged newValue must come from the last event.");
            Assert.AreEqual(ChangeKind.PropertySet, e.path[0].changeKind);
        }

        [Test]
        public void Pack_Does_Not_Merge_When_Descendant_Change_Introduces_Conflict()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithBuffer(new SampleRoot());

            root.Inner = new SampleInner { Description = "X" };
            root.Inner.Description = "Y";
            root.Inner = new SampleInner { Description = "Z" };

            Assert.AreEqual(3, buffer.Snapshot.Count, "Precondition: three events expected.");
            
            buffer.Pack();
            
            IReadOnlyList<ChangeEventArgs> packed = buffer.Snapshot;
            Assert.AreEqual(3, packed.Count, "Conflict (ancestor/descendant) between first and last 'Inner' events should prevent merging.");
            Assert.AreEqual("Inner", packed[0].PathString);
            Assert.AreEqual("Inner.Description", packed[1].PathString);
            Assert.AreEqual("Inner", packed[2].PathString);
        }

        [Test]
        public void Pack_Merges_Interleaved_Unrelated_Paths_Independently()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithBuffer(new SampleRoot
            {
                Name = "0",
                Age = 0
            });

            root.Name = "N1";
            root.Age  = 1;
            root.Name = "N2";
            root.Age  = 2;
            root.Name = "N3";

            Assert.AreEqual(5, buffer.Snapshot.Count, "Precondition failed: expected 5 raw events.");
            
            buffer.Pack();
            
            IReadOnlyList<ChangeEventArgs> packed = buffer.Snapshot;
            Assert.AreEqual(2, packed.Count, "Expected two merged events (Name, Age).");

            ChangeEventArgs nameEvent = packed.First(e => e.PathString == "Name");
            Assert.AreEqual("0", nameEvent.oldValue.Get<string>());
            Assert.AreEqual("N3", nameEvent.newValue.Get<string>());

            ChangeEventArgs ageEvent = packed.First(e => e.PathString == "Age");
            Assert.AreEqual(0, ageEvent.oldValue.Get<int>(), "Initial default int oldValue should be 0.");
            Assert.AreEqual(2, ageEvent.newValue.Get<int>());
        }
        
        private (SampleRoot root, ChangeLogBuffer buffer) NewRootWithCoalescingBuffer(SampleRoot plain,
            int scanLimit = ChangeLogBuffer.DefaultCoalesceAddScanLimit)
        {
            ChangeLogBuffer buffer = new ChangeLogBuffer();
            TrackableSampleRoot root = plain.AsTrackable();
            root.Changed += OnChange;
            return (root, buffer);
            void OnChange(object _, in ChangeEventArgs e) => buffer.AddCoalescing(e, scanLimit);
        }

        [Test]
        public void AddCoalescing_Merges_Sequential_Property_Changes_For_Same_Path()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithCoalescingBuffer(new SampleRoot
            {
                Name = "0"
            });

            root.Name = "A";
            root.Name = "B";
            root.Name = "C";

            IReadOnlyList<ChangeEventArgs> snapshot = buffer.Snapshot;
            Assert.AreEqual(1, snapshot.Count, "On-the-fly coalescing should keep only one Name event.");

            ChangeEventArgs e = snapshot[0];
            Assert.AreEqual("Name", e.PathString);
            Assert.AreEqual("0", e.oldValue.Get<string>(), "OldValue must remain from the initial state.");
            Assert.AreEqual("C", e.newValue.Get<string>(), "NewValue must be the last assigned value.");
            Assert.AreEqual(ChangeKind.PropertySet, e.path[0].changeKind);
        }

        [Test]
        public void AddCoalescing_Does_Not_Merge_When_Descendant_Change_Introduces_Conflict()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithCoalescingBuffer(new SampleRoot());
            
            root.Inner = new SampleInner { Description = "X" };
            root.Inner.Description = "Y";
            root.Inner = new SampleInner { Description = "Z" };

            IReadOnlyList<ChangeEventArgs> snapshot = buffer.Snapshot;
            Assert.AreEqual(3, snapshot.Count, "Ancestor/descendant barrier must prevent cross-merge.");
            Assert.AreEqual("Inner", snapshot[0].PathString);
            Assert.AreEqual("Inner.Description", snapshot[1].PathString);
            Assert.AreEqual("Inner", snapshot[2].PathString);
        }

        [Test]
        public void AddCoalescing_Merges_Interleaved_Unrelated_Paths_Independently()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithCoalescingBuffer(new SampleRoot
            {
                Name = "0",
                Age = 0
            });

            root.Name = "N1";
            root.Age  = 1;
            root.Name = "N2";
            root.Age  = 2;
            root.Name = "N3";

            IReadOnlyList<ChangeEventArgs> snapshot = buffer.Snapshot;
            Assert.AreEqual(2, snapshot.Count, "Expected two coalesced events (Name, Age).");

            ChangeEventArgs nameEvent = snapshot.First(e => e.PathString == "Name");
            Assert.AreEqual("0", nameEvent.oldValue.Get<string>());
            Assert.AreEqual("N3", nameEvent.newValue.Get<string>());

            ChangeEventArgs ageEvent = snapshot.First(e => e.PathString == "Age");
            Assert.AreEqual(0, ageEvent.oldValue.Get<int>(), "Initial default int oldValue should be 0.");
            Assert.AreEqual(2, ageEvent.newValue.Get<int>());
        }

        [Test]
        public void AddCoalescing_Respects_ScanLimit_DoesNotMerge_Beyond_Limit()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithCoalescingBuffer(new SampleRoot
            {
                Name = "0",
                Age = 0
            }, 1);
            
            root.Name = "N1";
            
            root.Age = 1;
            
            root.Name = "N2";

            IReadOnlyList<ChangeEventArgs> snapshot = buffer.Snapshot;
            
            Assert.AreEqual(3, snapshot.Count, "Expect two Name events (not merged due to scanLimit) and one merged Age event.");
            
            var nameEvents = snapshot.Where(e => e.PathString == "Name").ToArray();
            Assert.AreEqual(2, nameEvents.Length, "Two separate Name events expected due to scanLimit.");
            
            Assert.AreEqual("0", nameEvents[0].oldValue.Get<string>());
            Assert.AreEqual("N1", nameEvents[0].newValue.Get<string>());

            Assert.AreEqual("N2", nameEvents[1].newValue.Get<string>());

            ChangeEventArgs ageEvent = snapshot.First(e => e.PathString == "Age");
            Assert.AreEqual(0, ageEvent.oldValue.Get<int>());
            Assert.AreEqual(1, ageEvent.newValue.Get<int>());
        }

        [Test]
        public void AddCoalescing_Partial_Session_Before_Unrelated_Events_IsMerged()
        {
            (SampleRoot root, ChangeLogBuffer buffer) = NewRootWithCoalescingBuffer(new SampleRoot
            {
                Name = "0",
                Age = 0
            });
            
            root.Name = "A1";
            root.Name = "A2";
            root.Name = "A3";
            root.Age  = 1;
            root.Age  = 2;
            root.Age  = 3;
            root.Name = "A4";

            IReadOnlyList<ChangeEventArgs> snapshot = buffer.Snapshot;
            Assert.AreEqual(2, snapshot.Count, "A and B should be independently coalesced.");

            ChangeEventArgs nameEvent = snapshot.First(e => e.PathString == "Name");
            Assert.AreEqual("0", nameEvent.oldValue.Get<string>());
            Assert.AreEqual("A4", nameEvent.newValue.Get<string>());

            ChangeEventArgs ageEvent = snapshot.First(e => e.PathString == "Age");
            Assert.AreEqual(0, ageEvent.oldValue.Get<int>());
            Assert.AreEqual(3, ageEvent.newValue.Get<int>());
        }
    }
}