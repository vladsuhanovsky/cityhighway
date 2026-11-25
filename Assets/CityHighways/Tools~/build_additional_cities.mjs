// Deterministic schematic maps, not geographic surveys. Node 18+.
import fs from "node:fs";
import path from "node:path";
import { fileURLToPath } from "node:url";
const specs=[
  {
    "key": "paris",
    "name": "Париж",
    "en": "Paris",
    "river": "Сена",
    "riverEn": "Seine",
    "scale": 0.9,
    "riverY": [
      0.52,
      0.53,
      0.51,
      0.47,
      0.46,
      0.49,
      0.51,
      0.48
    ],
    "districts": [
      [
        "center",
        "Центр",
        "Centre"
      ],
      [
        "west",
        "Запад",
        "West"
      ],
      [
        "north",
        "Север",
        "North"
      ],
      [
        "east",
        "Восток",
        "East"
      ],
      [
        "south",
        "Юг",
        "South"
      ],
      [
        "latin",
        "Латинский квартал",
        "Latin Quarter"
      ],
      [
        "museum",
        "Музейный квартал",
        "Museum Quarter"
      ],
      [
        "business",
        "Дефанс",
        "La Défense"
      ],
      [
        "airports",
        "Аэропорты",
        "Airports"
      ],
      [
        "port",
        "Берси",
        "Bercy"
      ],
      [
        "parks",
        "Парки",
        "Parks"
      ],
      [
        "versailles",
        "Версаль",
        "Versailles",
        true
      ],
      [
        "denis",
        "Сен-Дени",
        "Saint-Denis",
        true
      ],
      [
        "vincennes",
        "Венсен",
        "Vincennes",
        true
      ]
    ],
    "rows": [
      [
        [
          "p01",
          "Нантер",
          "Nanterre",
          "business"
        ],
        [
          "p02",
          "Ла Дефанс",
          "La Défense",
          "business"
        ],
        [
          "p03",
          "Клиши",
          "Clichy",
          "north"
        ],
        [
          "p04",
          "Монмартр",
          "Montmartre",
          "north"
        ],
        [
          "p05",
          "Стад де Франс",
          "Stade de France",
          "denis"
        ],
        [
          "p06",
          "Сен-Дени",
          "Saint-Denis",
          "denis"
        ],
        [
          "p07",
          "Бурже",
          "Le Bourget",
          "airports"
        ]
      ],
      [
        [
          "p08",
          "Нёйи",
          "Neuilly",
          "west"
        ],
        [
          "p09",
          "Триумфальная арка",
          "Arc de Triomphe",
          "west"
        ],
        [
          "p10",
          "Сен-Лазар",
          "Saint-Lazare",
          "north"
        ],
        [
          "p11",
          "Опера",
          "Opéra",
          "center"
        ],
        [
          "p12",
          "Северный вокзал",
          "Gare du Nord",
          "north"
        ],
        [
          "p13",
          "Бельвиль",
          "Belleville",
          "east"
        ],
        [
          "p14",
          "Руасси",
          "Roissy",
          "airports"
        ]
      ],
      [
        [
          "p15",
          "Булонский лес",
          "Bois de Boulogne",
          "parks"
        ],
        [
          "p16",
          "Трокадеро",
          "Trocadéro",
          "west"
        ],
        [
          "p17",
          "Конкорд",
          "Concorde",
          "museum"
        ],
        [
          "p18",
          "Лувр",
          "Louvre",
          "center"
        ],
        [
          "p19",
          "Шатле",
          "Châtelet",
          "center"
        ],
        [
          "p20",
          "Бастилия",
          "Bastille",
          "east"
        ],
        [
          "p21",
          "Насьон",
          "Nation",
          "east"
        ]
      ],
      [
        [
          "p22",
          "Сен-Клу",
          "Saint-Cloud",
          "west"
        ],
        [
          "p23",
          "Эйфелева башня",
          "Eiffel Tower",
          "museum"
        ],
        [
          "p24",
          "Дом инвалидов",
          "Les Invalides",
          "museum"
        ],
        [
          "p25",
          "Орсе",
          "Orsay",
          "museum"
        ],
        [
          "p26",
          "Сен-Мишель",
          "Saint-Michel",
          "latin"
        ],
        [
          "p27",
          "Аустерлиц",
          "Austerlitz",
          "port"
        ],
        [
          "p28",
          "Берси",
          "Bercy",
          "port"
        ]
      ],
      [
        [
          "p29",
          "Версаль",
          "Versailles",
          "versailles"
        ],
        [
          "p30",
          "Севр",
          "Sèvres",
          "versailles"
        ],
        [
          "p31",
          "Монпарнас",
          "Montparnasse",
          "south"
        ],
        [
          "p32",
          "Люксембург",
          "Luxembourg",
          "latin"
        ],
        [
          "p33",
          "Пантеон",
          "Panthéon",
          "latin"
        ],
        [
          "p34",
          "Тольбиак",
          "Tolbiac",
          "south"
        ],
        [
          "p35",
          "Венсен",
          "Vincennes",
          "vincennes"
        ]
      ],
      [
        [
          "p36",
          "Велизи",
          "Vélizy",
          "versailles"
        ],
        [
          "p37",
          "Исси",
          "Issy",
          "south"
        ],
        [
          "p38",
          "Ванв",
          "Vanves",
          "south"
        ],
        [
          "p39",
          "Монсури",
          "Montsouris",
          "parks"
        ],
        [
          "p40",
          "Жантийи",
          "Gentilly",
          "south"
        ],
        [
          "p41",
          "Иври",
          "Ivry",
          "port"
        ],
        [
          "p42",
          "Венсенский лес",
          "Bois de Vincennes",
          "vincennes"
        ]
      ]
    ]
  },
  {
    "key": "berlin",
    "name": "Берлин",
    "en": "Berlin",
    "river": "Шпрее",
    "riverEn": "Spree",
    "scale": 0.85,
    "riverY": [
      0.47,
      0.46,
      0.49,
      0.52,
      0.54,
      0.5,
      0.46,
      0.45
    ],
    "districts": [
      [
        "center",
        "Центр",
        "Centre"
      ],
      [
        "west",
        "Шарлоттенбург",
        "Charlottenburg"
      ],
      [
        "north",
        "Веддинг",
        "Wedding"
      ],
      [
        "east",
        "Фридрихсхайн",
        "Friedrichshain"
      ],
      [
        "south",
        "Темпельхоф",
        "Tempelhof"
      ],
      [
        "parks",
        "Тиргартен",
        "Tiergarten"
      ],
      [
        "kreuzberg",
        "Кройцберг",
        "Kreuzberg"
      ],
      [
        "neukoelln",
        "Нойкёльн",
        "Neukölln"
      ],
      [
        "airports",
        "Аэропорты",
        "Airports"
      ],
      [
        "port",
        "Трептов",
        "Treptow"
      ],
      [
        "business",
        "Александерплац",
        "Alexanderplatz"
      ],
      [
        "spandau",
        "Шпандау",
        "Spandau",
        true
      ],
      [
        "pankow",
        "Панков",
        "Pankow",
        true
      ],
      [
        "koepenick",
        "Кёпеник",
        "Köpenick",
        true
      ]
    ],
    "rows": [
      [
        [
          "b01",
          "Цитадель",
          "Citadel",
          "spandau"
        ],
        [
          "b02",
          "Шпандау",
          "Spandau",
          "spandau"
        ],
        [
          "b03",
          "Юнгфернхайде",
          "Jungfernheide",
          "west"
        ],
        [
          "b04",
          "Веддинг",
          "Wedding",
          "north"
        ],
        [
          "b05",
          "Гезундбруннен",
          "Gesundbrunnen",
          "north"
        ],
        [
          "b06",
          "Панков",
          "Pankow",
          "pankow"
        ],
        [
          "b07",
          "Вайсензе",
          "Weißensee",
          "pankow"
        ]
      ],
      [
        [
          "b08",
          "Рулебен",
          "Ruhleben",
          "spandau"
        ],
        [
          "b09",
          "Вестенд",
          "Westend",
          "west"
        ],
        [
          "b10",
          "Шарлоттенбург",
          "Charlottenburg",
          "west"
        ],
        [
          "b11",
          "Моабит",
          "Moabit",
          "north"
        ],
        [
          "b12",
          "Хауптбанхоф",
          "Hauptbahnhof",
          "center"
        ],
        [
          "b13",
          "Пренцлауэр-Берг",
          "Prenzlauer Berg",
          "business"
        ],
        [
          "b14",
          "Лихтенберг",
          "Lichtenberg",
          "east"
        ]
      ],
      [
        [
          "b15",
          "Олимпийский стадион",
          "Olympic Stadium",
          "west"
        ],
        [
          "b16",
          "Бисмаркштрассе",
          "Bismarckstraße",
          "west"
        ],
        [
          "b17",
          "Тиргартен",
          "Tiergarten",
          "parks"
        ],
        [
          "b18",
          "Рейхстаг",
          "Reichstag",
          "center"
        ],
        [
          "b19",
          "Музейный остров",
          "Museum Island",
          "center"
        ],
        [
          "b20",
          "Александерплац",
          "Alexanderplatz",
          "business"
        ],
        [
          "b21",
          "Остбанхоф",
          "Ostbahnhof",
          "east"
        ]
      ],
      [
        [
          "b22",
          "Груневальд",
          "Grunewald",
          "parks"
        ],
        [
          "b23",
          "Курфюрстендамм",
          "Kurfürstendamm",
          "west"
        ],
        [
          "b24",
          "Зоологический сад",
          "Zoological Garden",
          "parks"
        ],
        [
          "b25",
          "Потсдамская площадь",
          "Potsdamer Platz",
          "center"
        ],
        [
          "b26",
          "Морицплац",
          "Moritzplatz",
          "kreuzberg"
        ],
        [
          "b27",
          "Гёрлицер-парк",
          "Görlitzer Park",
          "kreuzberg"
        ],
        [
          "b28",
          "Трептов-парк",
          "Treptower Park",
          "port"
        ]
      ],
      [
        [
          "b29",
          "Далем",
          "Dahlem",
          "parks"
        ],
        [
          "b30",
          "Штеглиц",
          "Steglitz",
          "south"
        ],
        [
          "b31",
          "Шёнеберг",
          "Schöneberg",
          "south"
        ],
        [
          "b32",
          "Темпельхоф",
          "Tempelhof",
          "south"
        ],
        [
          "b33",
          "Германплац",
          "Hermannplatz",
          "neukoelln"
        ],
        [
          "b34",
          "Зонненаллее",
          "Sonnenallee",
          "neukoelln"
        ],
        [
          "b35",
          "Кёпеник",
          "Köpenick",
          "koepenick"
        ]
      ],
      [
        [
          "b36",
          "Лихтерфельде",
          "Lichterfelde",
          "south"
        ],
        [
          "b37",
          "Мариендорф",
          "Mariendorf",
          "south"
        ],
        [
          "b38",
          "Букков",
          "Buckow",
          "neukoelln"
        ],
        [
          "b39",
          "Рудов",
          "Rudow",
          "airports"
        ],
        [
          "b40",
          "Шёнефельд",
          "Schönefeld",
          "airports"
        ],
        [
          "b41",
          "Адлерсхоф",
          "Adlershof",
          "port"
        ],
        [
          "b42",
          "Мюггельзее",
          "Müggelsee",
          "koepenick"
        ]
      ]
    ]
  }
];
function build(spec) {
 const round=x=>Math.round(x*1000)/1000, rows=spec.rows, locs=rows.flatMap((row,r)=>row.map(([id,name,en,district],c)=>({id,name,district,x:round(.075+c*.141+((r%2?1:-1)*(c%2?1:-1))*.008),y:round([.08,.23,.38,.63,.78,.92][r]+((c%2?1:-1)*(r%2?1:-1))*.008)})));
 const river=spec.riverY.map((y,i)=>({x:round(i/(spec.riverY.length-1)),y})), by=Object.fromEntries(locs.map(l=>[l.id,l])), expansion=new Set(spec.districts.filter(d=>d[3]).map(d=>d[0]));
 const cross=(a,b,c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x), inter=(a,b,c,d)=>cross(a,b,c)*cross(a,b,d)<-1e-12&&cross(c,d,a)*cross(c,d,b)<-1e-12;
 const px=p=>({x:p.x*1400,y:p.y*780}), distance=(p,a,b)=>{p=px(p);a=px(a);b=px(b);const dx=b.x-a.x,dy=b.y-a.y,t=Math.max(0,Math.min(1,((p.x-a.x)*dx+(p.y-a.y)*dy)/(dx*dx+dy*dy)));return Math.hypot(p.x-a.x-t*dx,p.y-a.y-t*dy)};
 const isBridge=(a,b)=>river.slice(1).some((p,i)=>inter(a,b,river[i],p));
 const roads=[], pairs=new Set(), add=(a,b)=>{const k=[a,b].sort().join("|");if(pairs.has(k))return;const p=by[a],q=by[b];if(locs.some(l=>l.id!==a&&l.id!==b&&distance(l,p,q)<24))return;if(roads.some(r=>![r.from,r.to].includes(a)&&![r.from,r.to].includes(b)&&inter(p,q,by[r.from],by[r.to])))return;
 const d=Math.hypot((p.x-q.x)*1400,(p.y-q.y)*780),length=d<95?1:d<150?2:d<210?3:d<300?4:5;const n=roads.length+1;const res=isBridge(p,q)?"steel":expansion.has(p.district)||expansion.has(q.district)?(n%3?"gravel":"asphalt"):n%7===0?"any":n%5===0?"gravel":n%2?"asphalt":"concrete";pairs.add(k);roads.push({id:"r"+String(n).padStart(2,"0"),from:a,to:b,length,resource:res})};
 for(let r=0;r<6;r++)for(let c=0;c<6;c++)add(rows[r][c][0],rows[r][c+1][0]);
 for(let r=0;r<5;r++)for(let c=0;c<7;c++)if(r!==2|| (spec.key==="paris"?c!==0&&c!==6:c!==1&&c!==5))add(rows[r][c][0],rows[r+1][c][0]);
 for(let r=0;r<5;r++)for(let c=0;c<6;c++)if((r*7+c+(spec.key==="paris"?0:1))%3===0){if((r+c)%2)add(rows[r][c+1][0],rows[r+1][c][0]);else add(rows[r][c][0],rows[r+1][c+1][0]);}
 const adj=Object.fromEntries(locs.map(l=>[l.id,[]]));for(const r of roads){adj[r.from].push([r.to,r.length]);adj[r.to].push([r.from,r.length]);}
 const members=Object.fromEntries(spec.districts.map(d=>[d[0],locs.filter(l=>l.district===d[0]).map(l=>l.id)]));
 const distances=(src,open)=>{const d=Object.fromEntries(locs.map(l=>[l.id,Infinity]));for(const u of members[src])if(open.has(by[u].district))d[u]=0;const todo=new Set(locs.filter(l=>open.has(l.district)).map(l=>l.id));while(todo.size){const u=[...todo].sort((a,b)=>d[a]-d[b]||a.localeCompare(b))[0];if(!Number.isFinite(d[u]))break;todo.delete(u);for(const [v,w] of adj[u])if(todo.has(v)&&d[u]+w<d[v])d[v]=d[u]+w;}return d};
 const open=new Set(spec.districts.filter(d=>!d[3]).map(d=>d[0])), all=new Set(spec.districts.map(d=>d[0]));const openDist=Object.fromEntries([...open].map(a=>[a,distances(a,open)]));
 const md=(d,b)=>Math.min(...members[b].map(id=>d[id]));
 const candidates=[];for(const a of open)for(const b of open)if(a<b){const d=md(openDist[a],b);if(d>=3&&Number.isFinite(d))candidates.push({a,b,d});}
 candidates.sort((a,b)=>a.d-b.d||a.a.localeCompare(b.a)||a.b.localeCompare(b.b));
 const contracts=[], points=d=>Math.max(4,d+1+(d>=9?2:0)+(d>=13?3:0)), contract=(a,b,d,kind,third)=>{if(!Number.isFinite(d))throw Error("unreachable contract "+a+" "+b);contracts.push({id:"c"+String(contracts.length+1).padStart(2,"0"),from:a,to:b,points:points(d)+(kind==="transit"?2:kind==="hub"?1:0),...(kind?{kind,third}:{})})};
 const count=Math.min(36,candidates.length);for(let i=0;i<count;i++){const c=candidates[Math.floor(i*(candidates.length-1)/(count-1))];contract(c.a,c.b,c.d)}
 for(const a of expansion){const d=distances(a,new Set([...open,a]));const targets=[...open].map(b=>({b,d:md(d,b)})).filter(x=>Number.isFinite(x.d)).sort((x,y)=>x.d-y.d||x.b.localeCompare(y.b));for(const t of [targets[Math.floor(targets.length/3)],targets[targets.length-2]])contract(a,t.b,t.d)}
 const openIds=[...open];for(let i=0;i<6;i++){const a=openIds[i],b=openIds[(i+5)%openIds.length],c=openIds[(i+8)%openIds.length];const d=i%2?md(openDist[a],b)+md(openDist[a],c):Math.min(...members[c].map(u=>openDist[a][u]+openDist[b][u]));contract(a,b,d,i%2?"hub":"transit",c)}
 const goals=[{id:"g1",name:"Воздушная гавань",kind:"locations",district:"airports",count:2,points:8},{id:"g2",name:"Город мостов",kind:"bridges",count:3,points:8},{id:"g3",name:"Вся агломерация",kind:"network",count:7,points:8},{id:"g4",name:"Дороги окраин",kind:"resource_pieces",resource:"gravel",count:9,points:6},{id:"g5",name:"Дальние трассы",kind:"long_roads",length:3,count:3,points:7},{id:"g6",name:"Сердце города",kind:"locations",district:"center",count:3,points:8},{id:"g7",name:"Портовый город",kind:"locations",district:"port",count:2,points:6}];
 for(const l of locs)if(river.slice(1).some((p,i)=>distance(l,river[i],p)<35))throw Error("wet location "+l.id);
 for(const s of [open,all,...[...expansion].map(a=>new Set([...open,a]))]){const d=distances("center",s);if(locs.some(l=>s.has(l.district)&&!Number.isFinite(d[l.id])))throw Error("disconnected "+spec.key)}
 if(roads.filter(r=>r.resource==="steel").length<5||contracts.length<40)throw Error("insufficient content "+spec.key);
 return {name:spec.name,districts:spec.districts.map(([id,name,en,expansion])=>({id,name,...(expansion?{expansion:true}:{})})),locations:locs,roads,contracts,river,rivers:[{name:spec.river,points:river}],goals,piecesScale:spec.scale};
}
const folder=process.argv[2] || path.resolve(path.dirname(fileURLToPath(import.meta.url)), "../Resources/CityHighways/Maps");
for (const spec of specs) {
 const data=build(spec);
 fs.writeFileSync(path.join(folder,spec.key+".json"),JSON.stringify(data,null,1)+"\n");
 console.log(spec.key,data.locations.length,data.roads.length,data.contracts.length);
}
