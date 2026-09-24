# Cheap syntax sanity check for environments without a C# compiler: reports any
# .cs file whose braces, parentheses or brackets do not balance outside strings
# and comments. Run from the project root: python3 Tools/Generators/check_braces.py
import glob, sys
def strip(s):
    out,i,n,mode=[],0,len(s),None
    while i<n:
        c=s[i]
        if mode is None:
            if c=='$' and s[i+1:i+2]=='"': out.append('$""'); mode='"'; i+=2; continue
            if c=='@' and s[i+1:i+2]=='"': mode='@'; out.append('""'); i+=2; continue
            if c=='"': mode='"'; out.append('""'); i+=1; continue
            if c=="'": mode="'"; out.append("''"); i+=1; continue
            if s[i:i+2]=='//': j=s.find('\n',i); i=n if j<0 else j; continue
            if s[i:i+2]=='/*': j=s.find('*/',i); i=n if j<0 else j+2; continue
            out.append(c); i+=1
        elif mode=='"':
            if c=='\\': i+=2; continue
            if c=='"': mode=None
            i+=1
        elif mode=='@':
            if s[i:i+2]=='""': i+=2; continue
            if c=='"': mode=None
            i+=1
        else:
            if c=='\\': i+=2; continue
            if c=="'": mode=None
            i+=1
    return ''.join(out)
bad=False
files=sorted(glob.glob("Assets/Scripts/**/*.cs",recursive=True))+glob.glob("Tests/Network/*.cs")
for f in files:
    t=strip(open(f,encoding='utf-8').read())
    b,p,k=t.count('{')-t.count('}'),t.count('(')-t.count(')'),t.count('[')-t.count(']')
    if b or p or k: bad=True; print("UNBALANCED",f,b,p,k)
print(f"{len(files)} files:", "PROBLEM" if bad else "balanced")
sys.exit(1 if bad else 0)
