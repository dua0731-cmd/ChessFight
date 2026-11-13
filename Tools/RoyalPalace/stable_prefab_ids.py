"""Stable Unity local IDs for generated, flattened prefabs.

Keys include the hierarchy identity AND Unity class/script identity. Adding unrelated
geometry cannot turn an existing component ID into a different type. External GUID
references are never rewritten. Duplicate sibling names use their sibling ordinal;
rename or reorder such siblings intentionally creates/reassigns their identities.
"""
import hashlib
import json
import re
from collections import defaultdict


def stabilize(text):
    records={int(m[2]):(m[1],m[0]) for m in re.finditer(
        r'^--- !u!(\d+) &(\d+)[^\n]*\n.*?(?=^--- !u!|\Z)',text,re.M|re.S)}
    def field(body,key):
        return int(re.search(r'^  '+key+r': \{fileID: (\d+)',body,re.M)[1])
    owners={field(b,'m_GameObject'):i for i,(typ,b) in records.items() if typ=='4'}
    names={i:re.search(r'^  m_Name: (.*)$',b,re.M)[1] for i,(typ,b) in records.items() if typ=='1'}
    paths={}
    def walk(tid,path):
        if tid in paths:raise ValueError('Duplicate or cyclic transform hierarchy')
        paths[tid]=path
        counts=defaultdict(int)
        for value in re.findall(r'^  - \{fileID: (\d+)\}',records[tid][1],re.M):
            child=int(value);name=names[field(records[child][1],'m_GameObject')]
            ordinal=counts[name];counts[name]+=1
            walk(child,path+((name,ordinal),))
    for tid,(typ,b) in records.items():
        if typ=='4' and field(b,'m_Father')==0:walk(tid,((names[field(b,'m_GameObject')],0),))
    keys={};component_counts=defaultdict(int)
    for go,tid in owners.items():
        for cid in re.findall(r'^  - component: \{fileID: (\d+)\}',records[go][1],re.M):
            cid=int(cid);typ,body=records[cid]
            script=re.search(r'm_Script: \{fileID: \d+, guid: (\w+)',body)
            identity=(typ,script[1] if script else '')
            index=component_counts[(go,identity)];component_counts[(go,identity)]+=1
            keys[cid]=(paths[tid],identity,index)
        keys[go]=(paths[tid],('1',''),0)
    if keys.keys()!=records.keys():raise ValueError('Prefab contains unsupported or unowned objects')
    mapping={};used=set()
    for old,key in keys.items():
        digest=hashlib.sha256(('war-room-v2:'+json.dumps(key,ensure_ascii=False)).encode()).digest()
        new=int.from_bytes(digest[:8],'big') & 0x7fffffffffffffff
        if new==0 or new in used:raise ValueError('Stable local ID collision')
        used.add(new);mapping[old]=new
    def replace_ref(m):
        old=int(m[1]);tail=m[2]
        if 'guid:' in tail or old==0:return m[0]
        if old not in mapping:raise ValueError('Unresolved local reference '+str(old))
        return '{fileID: '+str(mapping[old])+tail+'}'
    text=re.sub(r'\{fileID: (\d+)([^}]*)\}',replace_ref,text)
    text=re.sub(r'(^--- !u!\d+ &)(\d+)',lambda m:m[1]+str(mapping[int(m[2])]),text,flags=re.M)
    return text,mapping
