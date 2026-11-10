# 폰 러쉬 왕실 수로 정원 — 시험 씬 구역 G (R263)

> **R530(11-09):** 효과음 시작 — 비트 1 분수 광장: 회전 막대 축 · 휙 · 쿵(`GardenSpinBarSound`), 분수 6개 · 해자 물결(`GardenPlazaSound`), ElevenLabs → `plaza_sounds.py` → `Resources/PawnRushGardenSfx`. 전체 목록과 진행은 [정원 효과음](PAWN_RUSH_GARDEN_SFX.md). v160.

> **R491(11-06):** 나무 = `tree_asset.py`(source/tree_cone.glb · tree_round.glb → TreeAsset_v1.blend, 줄기 · 이끼 · 잎 2색 · 새로 만든 꽃) → `trees_sample.py`(테라스: 다트, 둑: 3~4개 숲, 성벽 · 탑 · 코스 피함) → export GardenTrees(손작업 후처리 없음). Unity `Concept` 조각 `GardenTrees`(덮기 · 충돌 없음). v159.

> **R490(11-06):** 성문 탑 판 옆 틈: 판 뒤에 몸통 속으로 마개(`PLUG_X` · `PLUG_BACK`, `gate_arch_asset.py`). v158.

> **R489(11-06):** 성문 테라스 화분 나무 8개 지움(`remove_topiaries.py`로 지금 blend에서, `walls_sample_v2.py`에서도). v157.

> **R488(11-06):** 탑 에셋 덤불 = 원뿔(`tower_asset.BUSHES`, `in_bush`: 반지름 1.35배 + 0.012, 발치 · 둘레 풀, 몸통 · 기둥 · 매끈한 돌 제외). 순서: tower_asset → walls_sample → handmade 2·3·4 → export GardenWalls. v156.

> **R487(11-06):** 호수 보트 4척 = 항로 2개(`lake_routes.py`: 남 117 m · 북 87 m, 자기 교차 검사) × 반 바퀴 간격 2척, 1.3 m/s(남 48 s · 북 36 s마다 한 척), `Lake boat south 1/2 · north 1/2`. 수문 · 수로에는 배 없음. v155.

> **R486(11-06):** 호수 보트 3척: `lake_routes.py`(세 물의 항로 · 장애물 여유 검사 · 평면도) → `GardenLakeBoat`(남 207 m · 서 176 m · 동 37 m, 0.45~0.9 m/s), `GardenWatergate.fbx`의 `Lake boat south / west / east`(파랑 · 진홍 · 아이보리), 물결은 `GardenBoatWater` → `_GardenBoatHulls*`(12칸: 저수지 8 + 호수). v154.

> **R485(11-06):** 동쪽 수로 수문: `gate_asset.py`(승규 님 castle gate GLB, 자리별로 칠함) → `watergate_sample_v1.py`(뒤집고 17.2배, 둑 높이 0.6, 물속 기초 2개, 통로 x 41.2~43.96) → `GardenWatergate.fbx`; 보트 `Watergate boat`(royal boat 0.75배)를 `GardenWatergateBoat`가 항로 83 m(통로 직선 + 양 끝 고리, `route_opt.py`)로 0.45~0.9 m/s, 약 107 s에 한 바퀴. 저수지 보트는 가라앉기 시작하면 물과 상호작용 없음(물보라 · 소리 뺌). v153.

> **R484(11-06):** 성문 수리: 배너 자리 = 닫힌 돌 판(`PANEL_*`, 받침 · 처마 속까지) + 그 위 뒷면 돌 켜(판 가장자리에서 bisect로 자름), 항아리 윗면 구멍 메움, 잎 칠 범위(갓돌 위 더미 · 그릇 둘레 · 덩굴, `URN_X` 0.40 · `urn_r`), 12.4만 면. v152.

> **R483(11-06):** 출구 성문 = 승규 님 성문 에셋(`gate_arch_asset.py` → `GateArchAsset_v1.blend`: 깃발 · 배너 삭제, 배너 자리 = 탑 뒷면 돌 켜 거울 복사 4 cm 앞, 수평 색 경계 자른 뒤 부위별 GC 재질, 9.35만 면), `gate_sample_v2.ASSET_GATE`(50.6배, (0, 66.5, 2.8), 받침대 북쪽 5.8 m; 손으로 만든 문은 안 넣음). 순서: gate_arch_asset → gate_sample_v2 → handmade 2·3·4 → export GardenGate. 충돌체 `Beats.Gate`(문설주 면 7.1, 탑 x 9.2~14.1, 화단 `Exit gate wing planter *`). v151.

> **R482(11-06):** 보트 ↔ 물: `GardenBoatWater`(B6 Boats, `_GardenHulls*` 8칸 → `PR_GardenClearWater`의 항적 · 고리 · 선체 물), `GardenBoatRock`(장식 흔들림, ObstacleClock), 잠수 · 부상 물보라(`GardenSplashFx`). v150.

> **R481(11-06):** 성벽 탑 = 승규 님 에셋(`tower_asset.py` → `TowerAsset_v1.blend`, 깃발 삭제 · 부위별 GC 재질 · 1.8만 면) × 12, `walls_sample_v2.asset_tower`(정원 쪽 앞, 하나 걸러 뒤집기, 처마 ~18 m, 돌 받침). 순서: tower_asset → walls_sample → handmade 2·3·4 → export. v149.

> **R480(11-06):** 후처리 4단계: 정점 색 `HM`(R 색차 · G 때 · B 이끼 · A 물자국) → `PR_GardenWeathered.shader`(stone · tile · joint · grout · paving GC_ 재질을 바꿔 그림, 반복 지우기). 물 · 폭포 메시는 제 정점 색 유지. 순서: 견본 → handmade 2 → 3 → 4 → export. v148.

> **R479(11-06):** 후처리 3단계: 긴 부품 처짐 0.4~1.5 cm · 휨, 돌 블록 기울기 0.35° · 크기 ±1 %, 7 % 모서리 깨짐(2~4.5 cm). 8개 FBX, v147.

> **R478(11-06):** 손작업 후처리 `handmade_pass.py --step 2`(견본 스크립트 뒤, 내보내기 전에 꼭 다시): 작은 장식 묶음별 회전 ±15° · 기울기 · 크기 ±6% · 위치, 난간 기둥 ±12°, 원뿔 나무 ±0.3 m, 걸린 판은 제 평면 안에서만; 돌 · 바닥 · 물 · 분수는 그대로. 8개 FBX, v146.

> **R477(11-06):** 성벽 · 탑 돌 = `GC_castle_stone`(Stone_Gate × (1.0, .93, .86)), 문은 `GC_gate_stone` 그대로, v145.

> **R476(11-05):** 성벽 아치 = 관통 창(두 면 같은 켜, 문설주 · 홍예석이 벽 가운데서 만남, 관통 문턱), 돌 = 출구 문 석조(한 색, 2단 둥근 모서리, 9 cm 뒤 몰탈 심, 켜 1.3), `GardenWalls.fbx`, v144.

> **R475(11-05):** 성벽 고리 Unity 반영 — `GardenWalls.fbx`, 블록아웃 `Castle wall W1..E6`(회전 상자) · `Castle tower W1..E6` · `Castle bank W/E` · `Castle strip *`, 사각 벽 · 모서리 탑 · 북쪽 벽 지움, 마당 북쪽 난간 · 출구 테라스 난간 60.5부터 · 문 날개 충돌, `GardenGate.fbx` 다시(테라스는 GardenWalls로), v143.

> **R474(11-05):** 외곽 성벽 견본 v2 `walls_sample_v2.py`: 초타원 고리(54 × 66.81, 지수 2.4, 중심 z 4.21), 탑 한쪽 6개(첫 탑 테라스 모서리 (±44.4, 48.6) → (±52.9, 23.2) → (±53.9, −3.7) → (±49.1, −30.2) → (±34.2, −52.2) → 입구 탑 (±9.5, −62.2)), 첫 벽은 문 날개 끝 블록 바깥 모서리 (±24.82, 67.72)에서, 아치 20개(해자 위 포함), 갈라짐 · 깨짐, 문 · 저수지 테라스는 고리에 잘려 이 견본에서, 고리 밖 잔디. `check_coplanar.py`는 모든 방향 · 실제 공통 면적. Unity 미반영.

> **R472(11-05):** 외곽 성벽 견본 `walls_sample_v1.py`: 사각 벽 대신 둥근 탑 22개 고리(문 날개 → 양옆 최대 x ±50.4 → 남쪽 입구 탑 x ±9.3), 흉벽 · 아치 · 깃발천, 둑(0.6) · 북쪽 띠(2.8), 옛 북쪽 벽 없음. Unity 미반영(붙일 때 셸 블록아웃 교체).

> **R467(11-05):** 출구 문 꽃 없음(`GATE_FLOWERS = False`; 항아리 · 화단은 흙만).

> **R465(11-05):** 출구 문 꽃 = `round_leaf` · `blossom`(겹꽃) · 줄기 덩굴 · 꽃무리, GardenGate.fbx 갱신.

> **R462(11-05):** B7 Unity 반영 — `GardenGate.fbx`, 앞쪽 개선문 블록아웃 지움, 출구 문 충돌체(문설주 x ±6..8.6 · 어깨 · 탑 x ±8.6..13.5 · 아치 꼭대기 18.2..24), v142.

> **R460(11-05):** 출구 문 돌 = `Stone_Gate`(그림 색) · `gate_run`(켜 1.7 m, 둥근 모서리, 얇은 줄눈); 키트 벽(켜 0.68, 세 색, 어두운 줄눈)과 다름 — "문만 이대로": 정원 나머지는 키트 돌 유지.

> **R458(11-05):** B7 v2 `gate_sample_v2.py`: 앞쪽 개선문 없음, 출구 문 = 승규 님 레퍼런스(탑 · 반원 아치 · 방패 · 깃발 · 꽃), 개구부 12 m 기준 높이 31 m · 폭 49 m, Unity 미반영(붙일 때 출구 아치 충돌체 바꿈).

> **R456(11-05):** B7 문 · 출구 견본 `gate_sample_v1.py`(개선문 · 출구 아치 · 메달 · 계단 · 테라스), Unity 미반영.

> **R446(11-05):** 배는 사진대로 `royal_boat.py`에서 직접 모델링(안쪽 가구 포함, 덮개 · 기둥 없음; 반폭 1.199, 랜턴 5.38), 터널 처마 5.5(`TunnelEaveY`, 4.4였음 — 이제 탄 기물을 치지 않음, 배가 가라앉을 때 빠짐), 지붕 6.1..7.3. GardenBoats.fbx, v138.
> **R444(11-05):** 물보라 무작위는 물리 분포(포아송 · 로그정규 · 폰 미제스)로, `_GardenSplashes[16]`, 소리 3가지.

> **R442 물에 빠지면(11-05):** 물마다 `WaterZone`(보고 안 함: 떠오르기만) + `GardenSplashPool`(물보라) + 사망 구역(되살리기). 물보라 계산 · 효과는 `GardenSplashFx`, 물결은 물 셰이더 `_GardenSplash*`, 소리는 `splash_sound.py` → `GardenSplash.wav`. 카메라는 어떤 물에서도 수면 위(`OrbitCamera.FloorAt`).

> **R441 물 높이(11-05):** 저수지 3.0 / 호수 −0.55 / 해자 −1.9. 호수는 P1 통 발(가장 낮을 때 −0.40) 아래여야 함(`water_tiers.py`의 `assert`). 물 높이에 붙는 장식은 모두 `water_tiers`에서 읽음(광장 WATER, 발판 WATER_Z, 배 RES_Y). 저수지 남벽 충돌: 여수로 부분 = 마루 높이, 나머지 = 3.8.

> **R440 물 3층 Unity 반영(11-05):** `GardenWater.fbx` + `PR_GardenFallUpper/Lower.mat` + `GardenWater_Landings.json`(모두 `water_tiers.py` → `export_water.py` · `export_concept.py`). 빌더 `Water()`는 사망 구역만(해자 · 호수), 물 그림은 컨셉 장식. 물 높이를 바꿀 때: `water_tiers.py` → 샘플 · 두 export 다시 → `RoyalWaterGardenBuilder.EarlyWaterY/MiddleWaterY` 맞추기.

> **R439 물 3층(11-05):** 후반 저수지 1.95 / 중반 호수 −0.35 / 초반 해자 −1.2. 여수로(저수지 남벽 x −26..−10.4) · 둑(z −15.5, 수로길 동 · 서). 수치는 `ArtSource/PawnRush/GardenConcept/water_tiers.py`가 유일한 원천(Unity 쪽 착수선 · 사망 구역도 거기 숫자로). 샘플 `water_sample_v1.py`, 아직 Unity에 안 붙임.

> **R437 B6 Unity 반영(11-05):** `GardenBoats.fbx`; `ConceptPieces`의 `GardenBoats` 구간. 매달기 묶음 이름은 경로도 됨(`B6_BoatCrossing/Boats`).

> **R436 B6 샘플(11-05):** `boats_sample_v1.py`. 배 = `Boat L-n`(곤돌라, 갑판 3.8, 매달 것), 터널 = 보트 하우스(블록아웃 유리 폭포 `Boat tunnel falls`는 덮지 않음). 아직 Unity에 안 붙임.

> **R435 수정(11-05):** 로봇 팔은 북쪽으로 82°까지 들리므로 흔들림 면(로봇 x ±0.9, 둑 벽 뒤 포함)에는 아무것도 두지 않음: 돌기둥은 x +1.15..+2.95, 받침은 허브 동쪽 면에서 나오는 금 축. 서쪽 갠트리 기둥 · 보는 지움. 수문은 파란색(`GC_gate_blue`). 장식 면은 블록아웃 그림 · 서로와 같은 평면에 두지 않음(깜빡임).

> **R434 수정(11-05):** 로봇 충돌 = 그린 모양(`Robot hand` 1.8 m · 4.7 m 아래, `Robot pillar block n`), 허브 원통은 `hammer_asset._drum`으로 새로 그림. 정원 돌벽은 모두 이음 4 cm · 3 cm에 줄눈 심(뒤가 비쳐 보이지 않게). Unity는 뒷면을 그리지 않으므로 손으로 만든 면은 법선을 바깥으로(PITFALLS §38).

> **R433 B4 · B5 Unity 반영(11-05):** `GardenLockDyke.fbx`; 덮음 · 매달기 목록은 `RoyalWaterGardenBuilder.Concept.cs`의 `GardenLockDyke` 구간. 로봇 기둥(고정)은 갠트리 기둥 충돌체를 덮고, `Robot arm N`은 손잡이 · 공만 흔들림(허브 원통은 고정).

> **R432 B4 · B5 샘플(11-05):** `lock_dyke_sample_v1.py` + 승규 님 공 망치 에셋(`hammer_asset.py`, `source/ball_and_chain.glb`). 로봇 = `Robot pillar N`(고정, 갠트리 기둥 충돌체를 덮게 넓힘) + `Robot arm N`(손잡이 · 공, 움직이는 팔에 매달 것). 수문 = `Sluice gate 1..3`(매달 것). 블록아웃 `Lock water` · `Current arrow`는 덮지 않음. 아직 Unity에 안 붙임.

> **R431 B3 Unity 반영(11-05):** `GardenPlatforms.fbx`; `ConceptPieces`의 `covered` · `hung` · `removed` 목록(이름 `*` 허용)으로 구간을 더함.

> **R430 B3 v3(11-05):** 큰 발판 = 기둥 없는 레퍼런스 통(`platforms_sample_v3.py`, 비교 `compare_tub.py`). Unity에 붙일 때 `Bobbing platform P* stem`은 그림 · 충돌 모두 지움.

> **R429 B3 v2(11-05):** 큰 발판 = 레퍼런스의 금 테두리 청록 통(`platforms_sample_v2.py` `tub`), 새 재질 `GC | platform stone` · `teal paint` · `teal dark` · `crown blue`. 이후 B3 작업은 v2 기준.

> **R428 B3 견본(11-05):** `platforms_sample_v1.py` — 움직이는 장식은 블록아웃 움직이는 부품과 같은 이름의 따로 된 물체(Unity에서 매달기): `Bobbing platform P1..8` · `Shortcut post K1..3` · `Shortcut post K4 spin bar` · `Shortcut lotus L1..2`. 덮을 블록아웃(그림만 끔): `West landing` · `West landing rail N/S` · `Bobbing platform P* stem` · `Shortcut post K*`(K4 받침 · 줄기) · `Shortcut lotus L* stem`, 매다는 부품 아래 `Drum` · `Drum rim` · `Post` · `Post rim` · `Bar` · `Hub` · `Pad` · `Pad rim`.

> **R426 물기둥 = 바닥 분수 + 계산한 물(11-05):** 설비는 `canal_sample_v2.py`(평지 6 = 수반 · 노즐, 경사로 2 = 배수판 · 노즐), 물은 `Assets/Scripts/PawnRush/GardenJet.cs` ↔ `jet_math.py`(같은 식; 바꾸면 둘 다) · 재질 `GC_jet`(폭포 셰이더, 정점 색 g = 공기, a = 1 물줄기 / 0 물막). 미리보기 `jet_preview.py -- sheet` · `-- video`.

> **R424 B2 Unity 반영(11-05):** `export_concept.py -- CanalSample_v1.blend "Canal dressing" GardenCanal` → `GardenCanal.fbx`. `RoyalWaterGardenBuilder.Concept.cs`의 `ConceptPieces`에 구간을 하나씩 더함(FBX 이름 + 덮을 블록아웃 (묶음, 이름)).

> **R423 B2 수로길 견본(11-05):** `canal_sample_v1.py`(공통 `gc_kit.py`) — 타일 바닥(수로길 · 앞마당 · 경사로), 블록 물가 벽, 돌 난간 4줄(금 구슬 · 금 화분), 서쪽 큰 벽, 물기둥 8개 = 돌 고리 + 금 고리 + 쇠 격자. Unity 반영은 확인 뒤(덮을 블록아웃 이름은 HANDOFF R423).

> **R419 v4(11-05):** 키트 그림 색(픽셀로 잼) · 바닥: 곡선 타일 동심원 + 사각 타일(`Tile_Peach.png`, 0.6 m, 4 × 4), 헤링본 · 띠 · 별 · 원판 없음, 둥근 회전 축(짙은 돌 띠 · 금 잎), 금 양파 꼭지 기둥. `plaza_sample_v4.py` → `export_plaza_v4.py`. 이후 작업은 v4 기준.

> **R415 v3(11-05):** 레퍼런스 렌더대로 회전 막대(팔각 돌 축 · 잎 문양 · 금 지붕 · 나무결 통나무 · 리벳 금 띠 · 끝 마개) · 화단(둥근 블록 2단 + 갓돌, 패널 기둥, 금 돔/화분) · 꽃(꽃잎 5장 + 꽃술, 잎 덤불, 토피어리). `plaza_sample_v3.py` → `export_plaza_v3.py`(텍스처 · 색 · 반복은 재질의 `gc_texture` · `gc_tint` · `gc_tiling`). 약 34만 삼각형.

> **R412 Unity 반영(11-05):** `export_plaza_v2.py` → `Resources/PawnRushGardenConcept/GardenPlaza.fbx` · `Materials/GC_*.mat` · `Textures/Herringbone_PinkBeige.png`. `RoyalWaterGardenBuilder.Concept.cs` `ConceptDressing`: `Content/Concept dressing`(Y 180°)에 FBX, 재질은 이름으로 `GC_*` 교체, 덮인 광장 블록아웃(`Start yard` · `Start yard rail W/E` · `Fountain plaza` · `Plaza kerb*`)은 그림만 끔, `Spin bar N` 장식은 같은 이름 회전 부품의 자식(블록아웃 막대 · 허브 그림 끔). 다음 구간도 같은 방식: Blender 견본 → 내보내기 → `ConceptDressing`에 덮을 이름 추가.

> **R409 견본 v2(11-05):** `plaza_sample_v2.py` — 바닥 동심원 띠 · 가운데 별, 입구 기둥 2.1 m · 깃발 · 큰 화분, 통나무 r 0.175 · 금 마개(그린 끝 ≤ 충돌 길이), 높은 막대 금 끝판, 화단 받침띠 · 몰딩, 반투명 물줄기 · 물보라. 이후 작업은 v2 기준.

> **R408 컨셉 아트 입히기 — 견본 1 분수 광장(11-04):** 배치 · 충돌은 v31 그대로, 겉모습만 컨셉 아트로. Blender 원본 `ArtSource/PawnRush/GardenConcept/plaza_sample_v1.py`(실행 `blender -b --python plaza_sample_v1.py`, 같은 폴더에 `.blend` · 렌더 · `textures/`). 재질 이름 `GC | …`(크림 석재 3톤 · 금 · 나무 · 흙 · 잎 3톤 · 꽃 5색 · 로열 블루 · 물줄기 · 연잎 · 헤링본). 다음: 승규 님 확인 → 광장 FBX 내보내기 → 프리팹에 붙이고 블록아웃 렌더러 끄기(충돌 유지) → 수로길 · 착지 등에 같은 재질 복사.

> **R400 리모델링 시작점 — v31 블록아웃 + 정원 물(11-04):** 승규 님 "물 그래픽을 제외하고 완전 초기 상태" → (물만 남겼다가) "맵 구조가 안보이고 물만 보이는데?". 정원 프리팹 `Assets/Prefabs/PawnRushSections/Early_1-1_RoyalWaterGarden.prefab`의 내용 = `RoyalWaterGardenBuilder.Build`: main v31 블록아웃 전체(B1 ~ B7 · `Garden` · `ProgressPath`) + 물 그래픽 — v31의 납작한 파란 물 판 대신 `Garden/Water/Garden water surface`(FBX `RoyalWaterGarden.fbx`의 `V07 Water | lower moat | gentle wave`, x ±46.5 · z ±58.5 · y 약 −1.7, `Water`가 Y 180°), 낙사 판정 y −3.9 ~ −1.8(v31보다 0.9 m 아래, R307과 같음), 저수지 판 `PR_GardenWater`. **ChessFight > Pawn Rush > Build Early 1-1 Royal Water Garden**(`PawnRushEditor/SectionPrefabMenu.cs`, main에서 가져옴)이 프리팹 `Content`로 구움 → 프리팹 모드에서 보고 손으로 고침. 다시 누르면 `Content`를 새로 지으므로 손으로 고친 것이 사라짐(이미 있으면 물어봄). 구역 G(`Course01v4Builder.RoyalGarden`)는 이 프리팹만 놓음 — 아래 R263 ~ R309의 Blender 모델 얹기 · 블록아웃 숨기기는 더 안 함(코드에서 지움, git 기록에 있음). 정원 FBX(56 MB)는 물 메시 하나 때문에 계속 쓰임.

> **R309 Unity v33 — 남은 블록아웃 덮기:** `build_v33.py`가 v31 블록아웃 위에 모델을 더 얹음(묶음 `V33 | v31 layout dressing`, 이름 `V19 | V33 …`). 바닥 · 벽 · 둑 · 터널 · 계단 · 출구 = V19 벽돌 모듈 타일, 난간 = V21 기둥 + 갓돌, 섬 · 기둥 줄기 = 둥근 벽돌 기둥, 지름길 K1~K3 = 1.2 m 통 발판 · K4 = 2.6 m 통 + 회전봉. 새 회색 클레이: 배 8척 · 수문 판 3 · 연잎 2 · 회전봉 6 · 간헐천 테두리 8. 움직이는 것은 메시 역할의 `attach`로 코드 부품(`Boat L-N`, `Sluice gate n`, `Shortcut post Kn`, `Shortcut lotus Ln`, `Spin bar n/Arm k`, `Bobbing platform Pn`)에 붙음. V33 부품은 충돌체 없음. Version 34.

> **R307 Unity v32 — main v31 배치:** 구역 G는 main의 `Early_1-1_RoyalWaterGarden` 프리팹(v31, `RoyalWaterGardenBuilder`)을 짓고 그 위에 정원 모델을 얹음. 모델 좌표 = v31 구간 좌표(Blender x, y, z → 구간 x, z, y). 옮긴 모델: 성문 묶음 · 승선 블록(높이 5.0 → 3.8), 통 발판 → P1~P8 위(`attach`, 코드 발판의 자식), 로봇 팔 2대 → 둑 남쪽 x 22.5 · 10.5. 모델이 덮는 블록아웃은 렌더러만 끄고 충돌은 그대로. 지운 모델 · 알려진 겹침은 HANDOFF R307과 `ArtSource/PawnRush/Garden/v32_report.json`. 미확인.

> **R296 Unity v31:** v30과 같은 물체 · 이름 · 위치 · 충돌 역할, 삼각형만 646만 → 399만. V19 공유 메시 3개(블록 92개)가 21,404 → 2,600(35° 넘는 모서리 각지게), V16 지그재그 발판 6개 · 왕실 배 메시(배 2척)가 약 9.3만. 만든 곳 `build_v31.py` → `export_unity_v31.py` → `check_unity_export_v31.py`, Version 27.

> **R287 Unity v30:** 같은 이름 FBX/JSON(313메시). 출발 대기장 바닥 = `V19 | 출발_12명대기_12x9m` 한 장(옛 `… 0.0 / 0.1 / 1.0 / 1.1` 네 장 대신, box 충돌). 둥근 섬(`V02_장식정원_독립섬` · `V19 | V02_장식섬_벽` · 화단) 없음, 양옆 테라스 · 수조 · 폭포 · 나무는 y +10.5. 새 `V30 | castle water gate`(box) · `V30 | gate channel water`(물) · `V30 | parade boat PIVOT` 아래 `V30 | parade royal boat`(충돌 없음). 매니페스트 `parade`(track · fps 30 · frames 2111 · samples 7개씩 · halfLength 3.8 · halfWidth 1.94 · forward = 배 자기 +x) → `GardenBoatParade`(Time.time 재생, 출발 대기장 |x|<7 · −61<z<−50에 들어오면 2초 뒤, 광장 |x|<19 · z −16을 북쪽으로 넘으면, 최대 2번, 쉴 때 비활성). `GardenWaterInteraction` 배 3척 · 배 자기 기준 앞 방향 · 비활성 배 무시, 셰이더 전역 `_GardenHull*[3]`.

> **R286 Unity v29:** 같은 이름 FBX/JSON(391메시). 둘레 = `V29 | castle wall {변}{번호} {구간}`(구간 메시 공유, 충돌 없음) · `V29 | castle corner tower {SW/NW/NE/SE}` · 숨은 충돌 판 `V29 | castle collision {W/E/N/S}`(proxy: MeshCollider · 렌더러 끔) · `V29 | outer meadow`. 옛 `V17 | castle wall` · `V19 | 성벽_*` · `외벽_흉벽` · `V04_성벽_탑지붕` 없음. 원본 모델 `ArtSource/PawnRush/Garden/Castle/*.glb`.

> **R285 Unity v28:** 같은 이름 FBX/JSON(527메시). `V04_중반_분수` 없음, `V23 | garden/planter bushes · flowers`는 건널목 · 둥근 단 덤불을 빼고 바닥에 내림. 배 PIVOT 트랙은 키 프레임(벽 앞 y 38.96 · 45.52에서 쿵 튕김), 런타임 코드는 그대로 샘플을 재생.

> **R284 Unity v27:** 같은 이름 FBX/JSON. `V26 | spout shadow …` 면이 모두 벽 밖을 봄(뒷면이 잘려 막혀 보이던 4곳), 수조 물줄기는 난간벽 구멍 안에서 시작. 물 재질 = 색 E, 수조 물(`V22 | tank water`)은 `PR_GardenWaterShallow`(같은 셰이더, 물결 0.006m — 수조가 3cm 깊이). 물 셰이더 새 속성 `_BoatRingHeight` · `_BoatGlow`, `Interaction`이 선체에 쌓인 물 정도(`hull`)도 돌려줌. 배 드라이버 9cm · 0.06rad. `Version=19`. 커밋 안 함, Unity 확인 전.

> **R283 Unity v26:** 같은 이름 `RoyalWaterGarden.fbx`/`_Motion.json`(528메시). 분출구 = `V26 | spout stone …`(테두리 · 홈통 · 수조 난간벽, 상자 충돌체) + `V26 | spout shadow …`(어두운 입구, 색 유지 — `Coloured`에 `shadow`), 폭포 = `V26 | fall curtain/streams …`(소스 이름으로 `PR_GardenFallSheet`), 착수선 17개(`water_interaction_v26.json`). 물 셰이더: `_BodyDepth` · `_CrestColor` · `_ChurnColor` · `_ChurnReach`, 착수선 배열 `_GardenLanding*`(24), 배 `_GardenBoatInfo`(들썩임 m/s · 흔들림 rad/s). 폭포 셰이더: 큐 Transparent+5, `_GardenFoot`(해자 수면 높이) · `_PoolTint` · `_FootHeight`. 보트 드라이버 7cm · 0.03rad · 0.05rad. `Version=18`. Unity 확인 전.

> **R281 Unity v25:** `RoyalWaterGarden_v25.fbx`/`_Motion.json`. 폭포 = `V25 | fall curtain …` + `V25 | fall streams …`(정점 색: r 가로, g 진행도, b 시드, a 물줄기=1), `GardenWaterLook.ApplyBySource`가 소스 이름으로 `PR_GardenFallSheet` 연결. 저수지 수면 +0.24m(약 3.5m), 보트 피벗 드라이버로 위아래·앞뒤·좌우 흔들림. 물 셰이더는 흰 거품 없이 굴곡만: `BoatWaves`(켈빈 항적 · 선수/선미파 · 고리), `RingSlope`(폭포 · 발판). `_WakeHeight`. `Version=16`. Unity 확인 전.

> **R280 Unity v24:** `RoyalWaterGarden_v24.fbx`/`_Motion.json`(트랙13: 보트2 · 진자2 · 팔꿈치2 · 회전봉 · 지그재그 발판6, `bobbers` 발판 트랙 · 반지름3m). C발판 구역 24→28m: 입구 턱 y -13~-10 · 높이 1.95m, 경사로 y -20~-13, 발판 중심 (∓2.9, -7+4.4k), 윗면 2.05+0.5k(+0~0.7 흔들림, 위상 -60°k). 물 셰이더 Gerstner(`_WaveHeight/_WaveLength/_WaveHeading/_WaterDepth/_Choppiness`), 발판 고리 `_RippleHeight`. `Version=15`. Unity 확인 전.

> **R279 Unity v23:** `RoyalWaterGarden_v23.fbx`/`_Motion.json`(+`splashes` 폭포 착수선 9개, `boats` 선체 반길이·반폭·진행축). `GardenWaterInteraction`(빌더가 붙임)이 매 프레임 `_GardenBoat*`/`_GardenSplash*` 셰이더 전역 배열을 채워 `PR_GardenClearWater`가 항적·거품·고리를 그림(읽기만, 이동 없음). 재질 매핑: `V23 | waterfall sheet` → `PR_GardenFallSheet`, `V23 | fall spray` → `PR_GardenSpray`, 잎·꽃 이름은 거칠기 .92. 클레이 제외 단어: tree/water/bush/flower/fall/spray. 물은 `ZWrite On`(폭포 발이 물 밑으로 비치지 않게). `Version=14`. Unity 확인 전.

> **R278 Unity v22:** `RoyalWaterGarden_v22.fbx`/`_Motion.json`. 로봇팔 1.25배·IK 6초 루프(감기1초 → 0.85초 스윙, 길 바닥 위 0.25m → 반대편 0.75초 정지 → 1.8초 복귀 → 정지, 2번 팔 3초 늦게), `V22 | robot k elbow knuckle`(팔꿈치 자식)·`shoulder hinge`(고정). 테라스 수조 폭포를 수조 높이로, `V22 | tank water`(물 재질), `V22 | reservoir … spout`(박스 충돌). 물 재질 `PR_GardenWater`는 새 셰이더 `PR_GardenClearWater`(URP 깊이·불투명 텍스처 필요), 폭포 셰이더도 굴절형. 시작점은 가장 가까운 바닥(`TryFloor` 정렬). `Version=13`. Unity 확인 전.

> **R277 Unity v21:** `RoyalGarden`은 `RoyalWaterGarden_v21.fbx`/`_Motion.json`을 읽는다(v20 Resources 파일 삭제). 회전봉에 섞였던 경계석·허브 고리 조각을 고정 오브젝트로 분리(벽 뜯김 원인, [PITFALLS §25](../Environment/PITFALLS.md)), 남북 입구 절단면 제거 후 석조 기둥벽·갓돌·기둥(박스 충돌), 허브 고정 고리, 나무 12그루 → 사용자 GLB 2종(`ArtSource/PawnRush/Garden/source_glb/`, 색 유지·충돌 없음). 충돌 역할 `proxy`: 숨긴 높이장 `V21 | plaza collision proxy`가 메시 충돌체, 장식 광장과 보수 포장은 충돌 없음. `SeoungTestCourse.Version=12`. Unity 확인 전.

> **R276 Unity v20:** 이제 `RoyalGarden`은 v06 대신 `Resources/PawnRushGarden/RoyalWaterGarden_v20.fbx`와 `_Motion.json`을 읽는다. 보이는 메시418/동작15트랙, 24초 반복을 공유 시계로 물리 스텝마다 보간. 이동 루트 Kinematic/`IMovingSurface`, 경사/아치/동적 메시 충돌체와 정적 모듈 상자 충돌체, 새 물 슬롯·단색 URP 재질, 저수지 낙사 높이 갱신. 시험 씬 버전11: **ChessFight > SeoungTest > Build Pawn Rush Test Scene → Play**. Blender 조명/컴포지터/멀리 있는 배경은 시험 씬에 안 넣음. Core133 및 FBX/트랙 재가져오기 점검 통과, Unity 편집기 미연결로 Unity 컴파일/화면/승선/가동간섭/충돌/성능/사용자 확인 전. 아래 v06 설명은 이전 기록이며 최신은 `ArtSource/PawnRush/Garden/UNITY.md`.

> **R275 애니메이션:** v20 Blender에서 기물3개/후반 촛대 제거, 회전봉 시계방향8초·C발판 상하6초(0~0.7m)·어깨/팔꿈치 로봇팔6초·양팀 보트 반대위상 왕복12초(별도 x±6 통로에서 y38~45). 광장바닥/화단/팔 지지부 고정. 30fps 1~720프레임, 설명 `ArtSource/PawnRush/Garden/ANIMATION.md`. Unity/충돌/실제 승선/가동간섭/사용자 확인 전.

> **R274 나머지 구조물:** v19 Blender에 벽 GLB를 원형 성탑/기둥, 낮은 바닥/기단/테라스/승선장, 옹벽/경사로로 변형. 기존174개와 작은 기둥10개 교체 → 공유 메시 구조241개. 경사로 주 상단0→3m 및 기존 구조 범위 기준. 텍스처 없음, 원본 숨김 보존, C발판8/진자피벗2 유지 점검. 교체 목록 `structure_report_v19.json`. Unity/바닥 평탄성/충돌/동작/성능/사용자 확인 전.

> **R273 광장/진자 교체:** v18 Blender의 새 원형 광장은 외곽36m·주 바닥0m로 맞춤, 남북 입구와 연결 바닥 보완. 에셋에 회전봉/화단이 포함되어 원래 광장 요소는 숨김 보존. 보트 전 진자2개는 새 철구 에셋의 지지부/가동부를 분리, 가동부만 기존 피벗에 연결. 단색 클레이/텍스처 없음. 광장 회전봉은 통합 정적 메시이며 Blender 드라이버 미복원. Unity/실제 통과폭/충돌/애니메이션/사용자 확인 전.

> **R272 성문/성벽 배치 정정:** v17 Blender에 아치를 끝 지점(y55, 바닥5.042m)으로 옮겨 기존 성문 외곽 폭36m·두께4m·상단19.91m에 맞춤. 정사각형 벽을 길이6m·두께2m·높이6m로 변형해 기존 외곽벽 x±47에 20개씩 연결. 끝의 성문-탑 사이도 연결벽2개로 이음. 단색 재질, 기존 모델 숨김 보존. 렌더/바운딩 점검만; 실제 통과폭/충돌/Unity/사용자 확인 전.

> **R271 사용자 에셋:** v16 Blender에 GLB4종을 텍스처 없이 배치(아치1 · C발판8 · 팀 보트2 · 벽 끝 석조4). 클레이 재질1개/이미지 노드 없음. 기존 모델은 숨겨 보존, 새 보트는 팀 피벗에 연결. 렌더와 파일 재열기 점검만, Unity/승선 높이/충돌/동작/사용자 확인 전.

> **R270 광장 조명/색감:** v15 Blender에서 햇빛/하늘빛, 하늘 파랑, 채도와 대비를 조정. 같은 플레이 높이 카메라에서 v14와 비교, 원본 보존. 색 보정은 Blender5.2 컴포지터 그룹이며 Unity 포스트프로세싱으로 이전하지 않음. 렌더 확인만, 사용자/Unity 확인 전.

> **R269 광장 원경:** v14 Blender의 맵 바깥에 산2겹/구름4무리와 카메라 하늘색 변화를 추가. 성문 실루엣을 유지하도록 낮고 완만한 능선, 기존 조명/카메라/동선 유지. 카메라 배경용 하늘은 Unity 셰이더가 아님. Unity 이전/성능/사용자 확인 전.

> **R268 광장 석조:** v13 Blender에서 낮은 벽20개 · 화단 받침4개를 엇갈린 석재, 줄눈, 베벨, 갓돌로 다듬음. 기존 벽은 Blender에서 숨겨 보존. 작전 회의실 계열의 따뜻한 중성색 유지. 전체/확대 렌더로 비교, Unity 반영/사용자 확인 전.

> **R267 광장 화단:** `RoyalWaterGarden_v12.blend`에 입체 꽃잎/꽃술/줄기/잎과 다섯 가지 꽃 색 추가. v11 카메라와 조명으로 비교. Blender 아트 작업이며 Unity 모델/코드는 그대로, Unity 이전 및 성능 확인 전.

> **R266 광장 아트 기준 1차:** `ArtSource/PawnRush/Garden/RoyalWaterGarden_v11.blend`에 방사형 포장, 모서리/색 변화, 금속 거칠기, 햇빛과 하늘빛, 높이2.5m 카메라 추가. `plaza_player_v11.png`로 비교. Blender 렌더만 확인했으며 참고 이미지 수준까지 식생/벽/배경 디테일, Unity 반영은 후속. 기존 v10 원본 보존.

> **R265 Unity 물 연결(2026-11-01):** 정원 생성 직후 `GardenWaterLook.Apply`가 G09 수면을 `Resources/PR_GardenWater`, G10 폭포를 `Resources/PR_GardenWaterfall` 재질로 교체한다. 수면은 기존 `ChessFight/Pawn Rush Water` 셰이더(교차 이동 노멀 · 프레넬 하늘 반사 · 햇빛 반짝임), 폭포는 새 `ChessFight/Garden Waterfall` 셰이더(월드 좌표 · 세로 흐름)를 쓴다. 원본 폭포/꽃 합친 메시에는 UV가 없어 기존 UV용 폭포 셰이더를 직접 쓰면 안 된다. 슬롯만 교체해 꽃 · 석재 · 기존 거품/충돌을 유지한다. `SeoungTestCourse.Version = 10`: Play 시작 시 다시 조립하거나 **ChessFight > SeoungTest > Build Pawn Rush Test Scene**. FBX 배치와 낙사 범위는 기존 v06, Blender v10의 고저차/벽은 Unity에 옮기지 않음. Core 133개 통과(.NET 10); Unity 편집기 미연결로 Unity C#/셰이더 컴파일 · 화면 · 사람 확인 전.

> **R264 Blender 작업본(2026-11-01):** `ArtSource/PawnRush/Garden/RoyalWaterGarden_v10.blend`는 v06 FBX를 가져와 경사로와 C자 발판의 고저차, 작전 회의실 벽 패널, 잔물결 · 폭포 애니메이션과 사실적인 물 재질을 편집한 별도 작업본이다. 30fps, 1~120프레임. **Unity FBX와 시험 씬은 여전히 아래 v06이다.** 원래 `.blend`가 아니라 FBX에서 복원했으므로 회전봉 · 진자 · 보트의 Blender 드라이버는 없다. Blender 물 재질/애니메이션을 Unity에 옮기려면 별도 셰이더와 동작 구현, 높아진 물의 낙사 범위 · 이동 피벗 · 충돌 재점검이 필요하다. Blender 렌더 · 데이터 점검만, Unity/사용자 확인 전.

2026-11-01, 승규 님. 클라우드 채팅에서 Blender로 만든 정원 맵을 `seoungtest`의 폰 러쉬 시험 씬(`SeoungTest_PawnRush`)에 **도서관 컨베이어 코스(구역 E) 옆 구역 G**로 넣었다. Blender 배치 그대로의 시험용이며 최종 아트 · 밸런스가 아니다. `seoungtest`에 커밋 · 푸시, **Unity · 사람 확인 전.**

## 1. 원본과 버전

v06 당시 맵 원본(`.blend` · 스크립트 · 렌더)은 저장소 밖(승규 님 PC의 Git 제외 폴더, 클라우드 채팅 ZIP `ChessFight_Garden_v06_Claude.zip`)에 있었고 저장소에는 Unity FBX만 넣었다. 이후 v06 FBX에서 복원해 편집한 v10 Blender 작업본은 위 R264 경로에 있다.

| 버전 | 한 것 |
|---|---|
| v03 | 승규 님이 정한 그레이박스: 회전봉 광장 → 상승 지형 → 백팀 왼쪽 C자 발판 4개 · 흑팀 오른쪽 반대 C자 4개 → 진자길 합류 → 상단 승선장 · 팀 보트 → 성문 |
| v04 | 컨셉 그림대로 실제 형태(사암 벽 · 화단 · 청동 회전봉 · 청록 · 금 드럼 발판 · 파랑 · 금 해머 · 곤돌라 보트 · 폭포 · 분수 · 연잎). 판정 치수는 v03 그대로, 새 물체는 모두 장식(`role=decor`, `collision=none`, 이름 `V04_*`) |
| v05 | 벽 · 석조를 도서관 컨베이어 코스 FBX의 재질 `CLAY \| uniform neutral gray`(0.54 회색, 거칠기 0.78, 금속 0)로, **텍스처 없음**(모든 재질 단색). 내보낸 FBX의 클레이 재질 속성 13개가 도서관 FBX와 같음 |
| v06 | 촛대 모양(원뿔) 나무 32그루 지움(장식정원 · 테라스 사이프러스 28, 광장 화단 원뿔 4). 둥근 나무 12그루는 남김 |

## 2. 배치(존 좌표, m)

FBX는 Blender (x, y, z)를 Unity (−x, z, −y)로 들여온다([PITFALLS §24](../Environment/PITFALLS.md)). 도서관처럼 180° 돌린 피벗 아래에 두어 존 좌표 = **(Blender x, Blender z, Blender y)**: 오른쪽 x, 위 y, 진행 방향(출발 → 성문) +z. 크기 약 96 × 120 m.

| 곳 | z | 바닥 높이 y |
|---|---|---|
| 출발 대기(12 × 9) · 시험 씬 출발점 (0, 0, −57) | −60 ~ −51 | 0 |
| 회전봉 광장(지름 34, 가운데 z −35) | −52 ~ −18 | 0 |
| 광장 출구 경사 지형(폭 12) | −20 ~ −9 | 0 → 1.8 |
| 하부 단층(팀별 진입, 폭 26) | −9 ~ −6 | 1.8 |
| C자 발판(반지름 3.05): 백팀 x −5 · −9.6 · −9.6 · −5, 흑팀 대칭 | −3 · 2.5 · 9.5 · 15 | 2.24 · 2.64 · 3.04 · 3.44 |
| 상부 단층(합류, 폭 24) | 18 ~ 21 | 3.8 |
| 진자길(폭 8, 진자 z 22 · 27) | 21 ~ 30 | 3.8 |
| 공용 승선 대기장(20 × 7) · 팀 부두(x ±6) | 28 ~ 35 | 3.83 |
| 팀 보트(x ±6) | 38 ↔ 45 | 갑판 3.83, 물 2.1 |
| 도착 성문 앞마당(28 × 10.5) | 49.5 ~ 60 | 3.84 |

## 3. Unity 연결

| 무엇 | 어디 |
|---|---|
| 모델 | `Assets/Resources/PawnRushGarden/PawnRushRoyalWaterGarden.fbx`(Git LFS, 약 5 MB, 메시 419 · 빈 물체 5 · 재질 26) |
| 가져오기 설정(`.meta`) | 도서관 환경 FBX 설정을 바탕으로: 재질 내장(`materialImportMode: 1`, 도서관 벽과 같은 방식), 애니메이션 없음. **배율 1 · 단위 변환 켬**(이 FBX는 미터 단위 `UnitScaleFactor 100`; 도서관 FBX는 센티미터라 배율 0.01 · 단위 변환 끔), 메시 읽기 켬(Play 때 지어도 MeshCollider가 붙게) |
| 짓는 코드 | `Course01v4Builder.RoyalGarden.cs`(`RoyalGarden(b)`), 구역 목록 `Course01v4Builder.Zones.cs`(`E`, `G`), `SeoungTestCourse.Version` 8 → 9 |
| 자리 | 시험 씬이 구역을 x 방향 한 줄에 80 m 간격으로 세우므로 도서관(E)의 동쪽 80 m 옆. 남쪽 끝이 z 0 |
| 충돌체 | 모델의 메시마다 `MeshCollider`(그 메시 그대로). 장식(`V04_*`)과 물 두 장(`해자_시각영역_물판`, `V03_상단_수면_높이2.1m`)은 없음 |
| 물에 빠짐 | `KillVolume` 둘(1.5초 뒤 구역 출발점): 해자 전체(y −3 ~ −1.45), 위 저수지(x ±18, z 31 ~ 59, y 1 ~ 1.95) |

### 3.1 움직이는 것 — Blender 드라이버 → Unity

모두 `ObstacleClock` 시간의 순수 함수(절대 규칙 7), 기존 `Obstacle` 컴포넌트를 피벗에 붙이고 자식 메시가 따라 움직인다. 시계 0초 = Blender 1프레임. 위 → 아래로 볼 때 같은 방향으로 돌도록 각도 부호를 바꿨다(존 좌표가 Blender 좌표의 거울이라).

| 장애물 | Blender | Unity |
|---|---|---|
| 회전봉(팔 4개) `회전봉_PIVOT_Z_8초1회전` | Z축 (frame−1)·2π/240, 8초 한 바퀴 | `Spinner` 존 위쪽 축 −45°/초 |
| 진자 둘 `V02_필수진자_PIVOT`(.001) | Y축 0.65 rad·sin(2πt/8 + 0 · π) | `Pendulum` 존 진행 축, 진폭 −37.24°, 8초, 두 번째 위상 4초 |
| 팀 보트 둘 `V02_백팀_보트_PIVOT` · `V02_흑팀_보트_PIVOT` | y = 41.5 − 3.5 cos(2πt/12) | `Oscillator` 진행 축 ±3.5 m, 12초, 위상 9초(피벗을 가운데 41.5로 옮겨 놓음). 갑판에 선 폰은 `IMovingSurface`로 같이 감 |

## 4. 알고 있는 것 · 아직 안 한 것

- **진자 해머가 지지 기둥을 지나감:** 가장 크게 흔들릴 때(2 · 6초) 해머(x ±2.35 ~ 6.25)가 기둥(x ±4.38 ~ 5.23)과 겹친다. v03 배치부터 있던 것(그때 공 지름 3 m도 같음). Unity에서는 움직이는 쪽이 Kinematic이라 부딪히지 않고 겹쳐 보이기만 한다. 기둥을 벌리거나 진폭을 줄일지 결정 대기.
- **회전봉 높이:** 팔이 바닥에서 약 0.7 ~ 1.4 m(띠 포함)라 지금 폰 러쉬 점프로 넘기 어렵고 숙여 지나갈 수도 없다(v03 그대로). 의도인지 Play로 확인.
- 팀 전용 승선(보트의 `team_only`), 체크포인트, 결승 · 골 판정, 미션 광장, 난간, `NoClimbSurface`는 아직 없다. 떨어지면 구역 출발점으로 간다.
- 장식에는 충돌체가 없다(꽃 · 나무 · 폭포 · 분수 · 갓돌 · 진자 받침 기둥 · 회전 고리 포함).
- 맵을 Blender에서 다시 고치면 같은 이름으로 FBX를 바꿔 넣고 `SeoungTestCourse.Version`을 올린다. 피벗 · 물 이름이 바뀌면 `RoyalGarden`의 이름 목록도 고친다.
