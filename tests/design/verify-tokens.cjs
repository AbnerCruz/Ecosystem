// Run from the repository root: node tests/design/verify-tokens.cjs
const assert=require("node:assert/strict");
const fs=require("node:fs");
const {global:g,version}=JSON.parse(fs.readFileSync("docs/design/tokens.json","utf8"));
assert.equal(version,"1.0.0"); assert.equal(g.version,"2.0.0");
const luminance=h=>[1,3,5].map(i=>parseInt(h.slice(i,i+2),16)/255).map(v=>v<=.04045?v/12.92:((v+.055)/1.055)**2.4).reduce((s,x,i)=>s+x*[.2126,.7152,.0722][i],0);
const contrast=(a,b)=>{const x=luminance(a),y=luminance(b);return (Math.max(x,y)+.05)/(Math.min(x,y)+.05);};
let n=0;
const check=(a,b,min,desc)=>{n++;assert.ok(contrast(a,b)>=min,desc+": "+contrast(a,b).toFixed(2));};
for(const [mode,t] of Object.entries(g.themes)){
  for(const [name,p] of Object.entries({...t.accents,...Object.fromEntries(Object.entries(t.status).map(([k,v])=>["status."+k,v]))})){
    for(const k of ["9","10"])check(p[k],"#FFFFFF",4.5,mode+" "+name+" solid "+k);
    for(const k of ["1","3","4","5"])check(p["11"],t.neutral[k],4.5,mode+" "+name+" text "+k);
  }
  for(const k of ["1","3","4","5"]){
    for(const f of ["10","11","12"])check(t.neutral[f],t.neutral[k],4.5,mode+" text "+f+" on "+k);
    check(t.neutral["8"],t.neutral[k],3,mode+" control boundary "+k);
  }
}
const html=fs.readFileSync("site/design/index.html","utf8");
for(const id of ["theme-choice","accent-choice","density-choice"])assert.ok(html.includes('id="'+id+'"'),id);
for(const accent of Object.keys(g.themes.light.accents))assert.ok(html.includes('value="'+accent+'"'),accent);
console.log(n+" reference contrast pairs passed; controls present (static only).");
