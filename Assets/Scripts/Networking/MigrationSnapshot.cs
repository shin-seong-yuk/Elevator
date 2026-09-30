using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using UnityEngine;
namespace ElevatorGame
{
    // Checkpoint data contains values and stable IDs only, never old Unity instance IDs.
    [Serializable] public sealed class MigrationBody
    {
        public Vector3 position,velocity,angular;public Quaternion rotation;public bool kinematic,gravity;
        public static MigrationBody Read(Rigidbody b)=>new(){position=b.position,rotation=b.rotation,velocity=b.isKinematic?Vector3.zero:b.linearVelocity,angular=b.isKinematic?Vector3.zero:b.angularVelocity,kinematic=b.isKinematic,gravity=b.useGravity};
        public void Apply(Rigidbody b){b.transform.SetPositionAndRotation(position,rotation);b.position=position;b.rotation=rotation;b.isKinematic=kinematic;b.useGravity=gravity;if(!kinematic){b.linearVelocity=velocity;b.angularVelocity=angular;}var sync=b.GetComponent<Unity.Netcode.Components.NetworkTransform>();if(sync&&sync.IsSpawned&&sync.IsServer)sync.Teleport(position,rotation,b.transform.localScale);}
    }
    [Serializable] public sealed class MigrationGrip{public int hand;public string target;public Vector3 point;}
    [Serializable] public sealed class MigrationPlayer
    {
        public int slot;public ulong steam;public bool bot,alive,ready;public MigrationBody body,left,right;
        public float grip,release,impact,stun;public Vector3 aim;public MigrationGrip[] grips;
    }
    [Serializable] public sealed class MigrationProp
    {
        public int key,prefab,action,weapon=-1,holder=-1,born,ammo=-2;public bool persistent;
        public float actionAge,projectileTime;public int projectileMode;public MigrationBody body;
    }
    [Serializable] public sealed class MigrationValue
    {
        public string name,kind,text;public float number;
        // JsonUtility walks declared recursive field types even when the arrays are empty.
        // Store nested collections as a separate JSON payload to keep its type tree finite.
        public string childrenJson;
        [NonSerialized] MigrationValue[] cachedValues;
        public MigrationValue[] values
        {
            get => cachedValues ??= string.IsNullOrEmpty(childrenJson)
                ? Array.Empty<MigrationValue>() : JsonUtility.FromJson<MigrationValues>(childrenJson).items;
            set { cachedValues=value;childrenJson=value==null?"":JsonUtility.ToJson(new MigrationValues{items=value}); }
        }
    }
    [Serializable] public sealed class MigrationValues { public MigrationValue[] items; }
    [Serializable] public sealed class MigrationEvent{public int kind;public MigrationValue[] fields;}
    [Serializable] public sealed class MigrationJoint
    {
        public string id,owner,target;public bool spring,auto;public Vector3 anchor,connectedAnchor;
        public float force,torque,springForce,damper,min,max;
    }
    [Serializable] public sealed class MigrationSnapshot
    {
        public int sequence,epoch,phase,stage,floor,seed,winner,total,randomCalls,randomSeed,previousEvent;
        public float remaining,survival,elapsed;public ulong broken;public string title;
        public MigrationPlayer[] players;public MigrationProp[] props;public MigrationEvent[] events;public MigrationJoint[] joints;
        public float[] panelHealth;public int[] cooldownSlots;public float[] weaponCooldowns;
        public static MigrationSnapshot Capture(int sequence,int epoch)
        {
            var r=RoundManager.Instance;
            var snapshot=new MigrationSnapshot{sequence=sequence,epoch=epoch,phase=(int)r.Phase.Value,stage=(int)r.Stage.Value,floor=r.Floor.Value,seed=r.Seed.Value,winner=r.Winner.Value,total=r.TotalCount.Value,remaining=r.Remaining,broken=r.BrokenPanels.Value,title=r.EventTitle.Value.ToString(),survival=(float)GameSession.Instance.SurvivalSeconds,elapsed=(float)(Time.timeAsDouble-GameSession.Instance.StartedAt)};
            var props=UnityEngine.Object.FindObjectsByType<NetworkProp>(FindObjectsSortMode.InstanceID).Where(p=>p.IsActive).ToArray();
            var context=new MigrationContext(props);
            snapshot.players=RoundManager.Players().Select(p=>new MigrationPlayer{slot=p.Slot.Value,steam=p.SteamId.Value,bot=p.IsBot.Value,alive=p.Alive.Value,ready=p.Ready.Value,body=MigrationBody.Read(p.Body),left=MigrationBody.Read(p.Grab.leftHand.GetComponent<Rigidbody>()),right=MigrationBody.Read(p.Grab.rightHand.GetComponent<Rigidbody>()),grip=p.Grab.GripStrength,release=p.Grab.ReleaseRemaining,impact=p.Grab.ImpactRemaining,stun=p.StunRemaining,aim=p.AimDirection,grips=p.Grab.CaptureGrips(context)}).ToArray();
            snapshot.props=props.Select((p,i)=>{var w=p.GetComponent<WeaponPickup>();var projectile=p.GetComponent<WeaponProjectile>();return new MigrationProp{key=p.MigrationKey,prefab=p.PrefabIndex,persistent=p.persistent,body=MigrationBody.Read(p.Body),action=p.Action.Value,actionAge=(float)(r.Clock-p.ActionAt.Value),weapon=w?w.Kind.Value:-1,holder=w?w.Holder.Value:-1,born=w?w.BornFloor.Value:0,ammo=w?w.RemainingAmmo:-2,projectileMode=projectile?projectile.MigrationMode:0,projectileTime=projectile?projectile.MigrationRemaining:0};}).ToArray();
            snapshot.joints=context.CaptureJoints();snapshot.events=r.events.CaptureMigration(context);
            snapshot.randomSeed=r.events.RandomSeed;snapshot.randomCalls=r.events.RandomCalls;snapshot.previousEvent=(int)r.events.PreviousEvent;
            snapshot.panelHealth=new float[40];foreach(var p in UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))snapshot.panelHealth[p.index]=p.Health;
            r.GetComponent<WeaponSystem>().CaptureCooldowns(out snapshot.cooldownSlots,out snapshot.weaponCooldowns);
            return snapshot;
        }
        public void Restore(Func<ulong,ulong?> ownerForSteam)
        {
            var r=RoundManager.Instance;r.events.Cleanup(true);r.GetComponent<WeaponSystem>().ResetAll();
            r.Phase.Value=RoundPhase.Lobby;
            foreach(var saved in players)
            {
                ulong? owner=saved.bot?0:ownerForSteam(saved.steam);if(!owner.HasValue)continue;
                var p=NetworkGameManager.Instance.SpawnRestored(owner.Value,saved);
                saved.body.Apply(p.Body);saved.left.Apply(p.Grab.leftHand.GetComponent<Rigidbody>());saved.right.Apply(p.Grab.rightHand.GetComponent<Rigidbody>());
                p.Grab.RestoreStrength(saved.grip,saved.release,saved.impact);p.RestoreControl(saved.aim,saved.stun);
            }
            var restored=new Dictionary<int,NetworkProp>();
            foreach(var saved in props)
            {
                if(saved.prefab<0||saved.prefab>=r.events.propPrefabs.Length)continue;
                var p=r.events.SpawnProp(saved.prefab,saved.body.position,Vector3.zero,saved.persistent);saved.body.Apply(p.Body);p.Action.Value=saved.action;p.ActionAt.Value=r.Clock-saved.actionAge;
                var w=p.GetComponent<WeaponPickup>();if(w){w.Kind.Value=saved.weapon;w.BornFloor.Value=saved.born;w.RestoreAmmo(saved.ammo==-2?w.Capacity:saved.ammo);w.Holder.Value=RoundManager.Players().Any(a=>a.Slot.Value==saved.holder)?saved.holder:-1;if(w.Holder.Value<0)p.Body.isKinematic=false;}
                var projectile=p.GetComponent<WeaponProjectile>();if(projectile)projectile.RestoreMigration(saved.projectileMode,saved.projectileTime);
                p.MigrationKey=saved.key;r.events.NextMigrationKey=Mathf.Max(r.events.NextMigrationKey,saved.key+1);restored[saved.key]=p;
            }
            var context=new MigrationContext(restored);context.RestoreJoints(joints);
            r.Floor.Value=floor;r.Seed.Value=seed;r.Winner.Value=winner;r.TotalCount.Value=total;r.BrokenPanels.Value=broken;
            foreach(var p in UnityEngine.Object.FindObjectsByType<CabinPanel>(FindObjectsSortMode.None))p.RestoreHealth(panelHealth[p.index]);
            foreach(var saved in players){var p=RoundManager.Players().FirstOrDefault(a=>a.Slot.Value==saved.slot);if(p)p.Grab.RestoreGrips(saved.grips,context);}
            r.events.RestoreMigration(events,context,floor,randomSeed,randomCalls,(EventKind)previousEvent);
            r.Stage.Value=(ElevatorStage)stage;r.StageEnds.Value=r.Clock+remaining;r.EventTitle.Value=new Unity.Collections.FixedString128Bytes(title);
            r.AliveCount.Value=RoundManager.Players().Count(p=>p.Alive.Value);r.elimination.ResetState();
            GameSession.Instance.RestoreTiming(elapsed,survival);r.GetComponent<WeaponSystem>().RestoreCooldowns(cooldownSlots,weaponCooldowns);
            r.Phase.Value=(RoundPhase)phase;
        }
    }
    public sealed class MigrationContext
    {
        readonly Dictionary<int,NetworkProp> props;readonly Dictionary<string,Joint> joints=new();
        public MigrationContext(NetworkProp[] all){props=all.ToDictionary(p=>p.MigrationKey,p=>p);}
        public MigrationContext(Dictionary<int,NetworkProp> restored){props=restored;}
        public string Reference(UnityEngine.Object obj)
        {
            if(!obj)return "";
            if(obj is Joint joint){var p=joint.GetComponent<NetworkProp>();if(!p)return "";return "joint:"+Reference(p)+":"+Array.IndexOf(p.GetComponents<Joint>(),joint);}
            if(obj is PlayerController actor)return "player:"+actor.Slot.Value;
            if(obj is NetworkProp prop)return "prop:"+props.First(pair=>pair.Value==prop).Key;
            if(obj is Component component)
            {
                var player=component.GetComponentInParent<PlayerController>();
                if(player)return "body:"+player.Slot.Value+":"+(component.transform==player.Grab.leftHand?"L":component.transform==player.Grab.rightHand?"R":"B");
                var owner=component.GetComponentInParent<NetworkProp>();if(owner)return Reference(owner);
                Transform t=component.transform;var indices=new List<int>();while(t.parent){indices.Add(t.GetSiblingIndex());t=t.parent;}indices.Reverse();return "scene:"+t.name+"|"+string.Join("/",indices);
            }
            return "";
        }
        public UnityEngine.Object Resolve(string key,Type type)
        {
            if(string.IsNullOrEmpty(key))return null;
            if(key.StartsWith("joint:"))return joints.TryGetValue(key,out var j)?j:null;
            GameObject go=null;string[] parts=key.Split(':');
            if(parts[0]=="prop"){if(props.TryGetValue(int.Parse(parts[1]),out var prop)&&prop)go=prop.gameObject;}
            else if(parts[0]=="player"||parts[0]=="body")
            {var p=RoundManager.Players().FirstOrDefault(a=>a.Slot.Value==int.Parse(parts[1]));if(p)go=parts.Length<3||parts[2]=="B"?p.gameObject:parts[2]=="L"?p.Grab.leftHand.gameObject:p.Grab.rightHand.gameObject;}
            else if(key.StartsWith("scene:"))
            {var path=key.Substring(6).Split('|');go=GameObject.Find(path[0]);if(go&&path.Length>1&&!string.IsNullOrEmpty(path[1]))foreach(string item in path[1].Split('/')){int i=int.Parse(item);if(i>=go.transform.childCount)return null;go=go.transform.GetChild(i).gameObject;}}
            return go?(go.GetComponent(type)??go.GetComponentInChildren(type)):null;
        }
        public MigrationJoint[] CaptureJoints()
        {
            var result=new List<MigrationJoint>();
            foreach(var prop in props.Values)foreach(var joint in prop.GetComponents<Joint>())
            {
                if(joint is not SpringJoint&&joint is not FixedJoint)continue;
                var spring=joint as SpringJoint;
                result.Add(new MigrationJoint{id=Reference(joint),owner=Reference(prop),target=Reference(joint.connectedBody),spring=spring,auto=joint.autoConfigureConnectedAnchor,anchor=joint.anchor,connectedAnchor=joint.connectedAnchor,force=joint.breakForce,torque=joint.breakTorque,springForce=spring?spring.spring:0,damper=spring?spring.damper:0,min=spring?spring.minDistance:0,max=spring?spring.maxDistance:0});
            }
            return result.ToArray();
        }
        public void RestoreJoints(MigrationJoint[] saved)
        {
            foreach(var s in saved){var owner=Resolve(s.owner,typeof(NetworkProp)) as NetworkProp;var target=Resolve(s.target,typeof(Rigidbody)) as Rigidbody;if(!owner||!target)continue;Joint joint=s.spring?owner.gameObject.AddComponent<SpringJoint>():owner.gameObject.AddComponent<FixedJoint>();joint.connectedBody=target;joint.autoConfigureConnectedAnchor=false;joint.anchor=s.anchor;joint.connectedAnchor=s.connectedAnchor;joint.breakForce=s.force;joint.breakTorque=s.torque;if(joint is SpringJoint spring){spring.spring=s.springForce;spring.damper=s.damper;spring.minDistance=s.min;spring.maxDistance=s.max;}joints[s.id]=joint;}
        }
        public MigrationValue Encode(object value,Type type)
        {
            var v=new MigrationValue();if(value==null){v.kind="null";return v;}
            if(type==typeof(float)||type==typeof(int)||type==typeof(bool)){v.kind="number";v.number=Convert.ToSingle(value);}
            else if(typeof(UnityEngine.Object).IsAssignableFrom(type)){v.kind="reference";v.text=Reference(value as UnityEngine.Object);}
            else if(value is IDictionary dictionary){v.kind="dictionary";var values=new List<MigrationValue>();foreach(DictionaryEntry item in dictionary){values.Add(Encode(item.Key,type.GetGenericArguments()[0]));values.Add(Encode(item.Value,type.GetGenericArguments()[1]));}v.values=values.ToArray();}
            else if(value is IList list){v.kind="list";Type element=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];v.values=list.Cast<object>().Select(item=>Encode(item,element)).ToArray();}
            else throw new InvalidOperationException("Unsupported migration field: "+type);
            return v;
        }
        public object Decode(MigrationValue v,Type type,object current=null)
        {
            if(v.kind=="null")return null;
            if(v.kind=="number")return Convert.ChangeType(v.number,type);
            if(v.kind=="reference")return Resolve(v.text,type);
            if(v.kind=="dictionary"){var result=current as IDictionary??(IDictionary)Activator.CreateInstance(type);result.Clear();for(int i=0;i<v.values.Length;i+=2){var key=Decode(v.values[i],type.GetGenericArguments()[0]);if(key is UnityEngine.Object obj&&!obj)continue;if(key!=null)result[key]=Decode(v.values[i+1],type.GetGenericArguments()[1]);}return result;}
            if(v.kind=="list"){Type element=type.IsArray?type.GetElementType():type.GetGenericArguments()[0];var list=type.IsArray?(IList)Array.CreateInstance(element,v.values.Length):current as IList??(IList)Activator.CreateInstance(type);if(!type.IsArray)list.Clear();for(int i=0;i<v.values.Length;i++){var item=Decode(v.values[i],element);if(type.IsArray)list[i]=item;else list.Add(item);}return list;}
            throw new InvalidOperationException("Invalid checkpoint field");
        }
        public MigrationValue[] CaptureFields(FloorEvent e)
        {
            var fields=new List<MigrationValue>();
            foreach(var f in Fields(e)){var v=Encode(f.GetValue(e),f.FieldType);v.name=f.Name;fields.Add(v);}return fields.ToArray();
        }
        public void RestoreFields(FloorEvent e,MigrationValue[] values){foreach(var f in Fields(e)){var v=values.FirstOrDefault(x=>x.name==f.Name);if(v==null)continue;var restored=Decode(v,f.FieldType,f.GetValue(e));if(!f.IsInitOnly)f.SetValue(e,restored);else if(f.FieldType.IsArray)Array.Copy((Array)restored,(Array)f.GetValue(e),((Array)restored).Length);}}
        static IEnumerable<FieldInfo> Fields(FloorEvent e)
        {
            for(Type type=e.GetType();type!=typeof(MonoBehaviour);type=type.BaseType)
                foreach(var f in type.GetFields(BindingFlags.Instance|BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.DeclaredOnly))
                    if(f.Name!="Manager"&&!f.Name.Contains("k__BackingField"))yield return f;
        }
    }
}


