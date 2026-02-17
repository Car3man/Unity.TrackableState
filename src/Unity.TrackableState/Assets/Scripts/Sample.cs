using System.Collections.Generic;
using System.Runtime.Serialization;
using Klopoff.TrackableState.Core;
using UnityEngine;

namespace SampleNamespace
{
    [Trackable]
    public class SampleInNamespace
    {
        [DataMember]
        public virtual string Name { get; set; }
    }
}

[Trackable(CopyAttributes = false)]
public class SampleInner
{
    [DataMember(Name = "description")]
    public virtual string Description { get; set; }
}

[Trackable]
public class SampleRoot
{
    [DataMember(Name = "name")]
    public virtual string Name { get; set; }
    
    [DataMember(Name = "age")]
    public virtual int Age { get; set; }
    
    [DataMember(Name = "inner")]
    public virtual SampleInner Inner { get; set; }
    
    [DataMember(Name = "inNamespace")]
    public virtual SampleNamespace.SampleInNamespace InNamespace { get; set; }
    
    [DataMember(Name = "tags")]
    public virtual IList<string> Tags { get; set; }
    
    [DataMember(Name = "properties")]
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