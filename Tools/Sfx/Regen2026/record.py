import json,sys
n=json.load(open("nodes.json"))["nodes"]; inv={v:k for k,v in n.items()}
s=json.load(open("sessions.json"))
for pair in sys.argv[1:]:
    node,sid=pair.split(":"); s[sid]=inv[node]
json.dump(s,open("sessions.json","w"),indent=1); print(len(s),"sessions")
