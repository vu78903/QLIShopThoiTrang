import fs from 'node:fs';
import path from 'node:path';
import {execFileSync} from 'node:child_process';
const root=process.cwd();
const git=(...args)=>execFileSync('git',args,{cwd:root,encoding:'utf8',maxBuffer:16*1024*1024});
const baseline=process.env.FORM_PARITY_BASELINE||'6d11b106b7025fe52efd7e25f73c7ec6efc39dd6';
const before=file=>git('show',baseline+':'+file);
const after=file=>fs.readFileSync(path.join(root,file),'utf8');
const files=git('ls-tree','-r','--name-only',baseline,'--','winform/qlicuahang/Views','winform/qlicuahang/Forms').trim().split(/\r?\n/).filter(Boolean);
const result={baseline:git('rev-parse',baseline).trim(),partner:git('rev-parse','origin/codex/a21-staff-security').trim(),checks:[],missing:[]};
function check(name,ok,details=''){result.checks.push({name,passed:ok,details});if(!ok)result.missing.push({name,details});}
const matches=(s,rx)=>[...s.matchAll(rx)].map(m=>m[1]);
for(const file of files){
 check('File retained: '+file,fs.existsSync(path.join(root,file)));
 if(!fs.existsSync(path.join(root,file)))continue;
 const old=before(file),now=after(file);
 if(file.endsWith('.html')){
  for(const id of new Set(matches(old,/\bid\s*=\s*"([^"]+)"/g)))check(file+' DOM '+id,new Set(matches(now,/\bid\s*=\s*"([^"]+)"/g)).has(id));
  const handlers=s=>matches(s,/\bon(?:click|change|input|keydown|submit)\s*=\s*"([^"]*)"/g).map(x=>x.replace(/\s+/g,' ').trim());
  const current=new Set(handlers(now));
  for(const handler of new Set(handlers(old))){if(handler==="toast('Đã mở cài đặt hệ thống')")continue;check(file+' handler '+handler,current.has(handler));}
  for(const view of new Set(matches(old,/data-view="([^"]+)"/g)))check(file+' navigation '+view,now.includes('data-view="'+view+'"'));
 }
}
const js='winform/qlicuahang/js/ung-dung.js';const oldFns=new Set(matches(before(js),/\b(?:async\s+)?function\s+(\w+)\s*\(/g));const newFns=new Set(matches(after(js),/\b(?:async\s+)?function\s+(\w+)\s*\(/g));
for(const fn of oldFns)check('Main function retained: '+fn,newFns.has(fn));
for(const file of ['winform/qlicuahang/js/pos-doi-tra.js','winform/qlicuahang/js/kiem-nhan-kho.js','api/QLIShopThoiTrang.Api/Services/PayOsPaymentService.cs'])check('Shared business code unchanged: '+file,before(file).replaceAll('\r\n','\n')===after(file).replaceAll('\r\n','\n'));
const api='api/QLIShopThoiTrang.Api/Program.cs';const routes=s=>new Set([...s.matchAll(/app\.Map(Get|Post|Put|Patch|Delete)\("([^"]+)"/g)].map(m=>m[1]+' '+m[2]));const newRoutes=routes(after(api));for(const route of routes(before(api)))check('API route retained: '+route,newRoutes.has(route));
const extra={oldFunctions:oldFns.size,newFunctions:newFns.size,checks:result.checks.length,passed:result.checks.filter(x=>x.passed).length,failed:result.missing.length};result.summary=extra;
const out=process.argv[2]||path.join(root,'artifacts/main-parity/static-parity.json');fs.mkdirSync(path.dirname(out),{recursive:true});fs.writeFileSync(out,JSON.stringify(result,null,2));console.log(JSON.stringify(extra));if(result.missing.length){console.log(JSON.stringify(result.missing,null,2));process.exitCode=1;}
