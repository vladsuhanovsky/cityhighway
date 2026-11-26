# Character Art 2.0 — production asset intake

Status: candidate research only; no purchase, download, licensed source or actual Humanoid runtime test yet. Unity project uses 6000.6.8f1 and URP. Do not equate store previews with in-game visual acceptance.

## Candidates (checked 2026-11-27)

- First evaluation: [Stylized Modular Characters 2.0 — ROCKETARTS](https://marketplace.unity.com/packages/3d/characters/humanoids/stylized-modular-characters-2-0-262315), $89.99 before tax on listing. Publisher describes shared Humanoid rig, male/female modular parts, URP PBR albedo/normal/metallic-smoothness/AO. Listed compatibility Unity 6000.0.62f1; exact project version and clothing coverage not verified. Extension asset: verify number of seats. Listing has a Created with AI section; verify actual disclosure and provenance with publisher before acquisition and Steam release.
- Lower-cost evaluation: [BoZo Professions Pack](https://assetstore.unity.com/packages/3d/characters/humanoids/bozo-stylized-modular-characters-professions-pack-309513), $40 before tax on listing. Publisher describes male/female blendable body, modular customization, Humanoid and Shader Graph. Listed URP compatibility 2022.3.62f1, not verified in our editor. Check visual style, actual construction wardrobe and texture maps.
- [Construction Workers Polyart](https://marketplace.unity.com/packages/3d/characters/construction-workers-polyart-characters-320656) is a possible city reference/LOD candidate only; nonmodular according to publisher. Avoid using it as the primary wardrobe model.
- [Adobe Mixamo FAQ](https://helpx.adobe.com/creative-cloud/faq/mixamo-faq.html): optional source of commercial-game animations. No raw file redistribution.

## License and repository boundary

[Unity Asset Store FAQ](https://assetstore.unity.com/browse/eula-faq) permits commercial use embedded in a game under applicable license, not standalone redistribution. Repository is public. Do not import licensed files into tracked directories, push vendor source, or put raw FBX, textures, animation clips, package files or derived wrappers with licensed geometry in public GitHub. Import into a separate private scratch project first, then move approved assets and all dependencies to Assets/CityHighwaysLicensedArt/ in local project; wrapper prefabs in Assets/CityHighways/Resources/CityHighways/Profile3DProfessional/. Both paths ignored. Check git status and dependencies before every commit; ignore rules do not protect assets elsewhere or previously tracked files. Private release pipeline and asset provenance records required. No secrets or invoices in public repo. Verify commercial game/Steam use, seats and AI disclosure before purchase.

## Authoring contract

1. Import two separate male/female bases in a private scratch project. Unity model import Rig: Humanoid, valid Avatar. Check materials in URP, bone weights, bind poses, scale, +Z forward and ground.
2. Build clean visual-only root prefab per gender; one Animator and shared bone hierarchy outside any clothing variant root. Bind each of 29 catalog IDs via ProfileAvatarRig inspector; roots may not be nested. Body/skin, hair, hats, top, bottom, shoes, accessories. No vendor runtime scripts, cameras, lights, physics or duplicate Animator. All SkinnedMeshRenderer bones must belong to same persistent skeleton. Vendor meshes need adapting, not arbitrary substitution of missing IDs. 
3. Add ProfileHumanoidArt to same root, assign Animator and Humanoid idle/walk clips (in-place, no root motion). Save Male.prefab/Female.prefab in ignored Professional Resources directory. Preview priority: validated complete professional rig, detailed procedural, legacy procedural, 2D. Manual PlayableGraph samples clips only in the profile preview; still = idle frame zero; reduced motion freezes. No vendor AnimatorController required. City uses old lightweight rig until LOD and real animation support there are implemented.
4. Use City Highways/Profile/Validate Professional Avatars for read-only preflight. Compare against original wardrobe mockup in Play Mode; verify both genders, all variants, layer/skin clipping, animation, visibility and teardown, render counts and materials. Actual humanoid graph, retargeting and licensing acceptance require real imported assets; public CI checks only absence/fallback and data contract.

## Required final acceptance

Professionally modeled bodies and garments, valid shared humanoid skeleton and skin weights, compatible materials with appropriate texture maps, all existing item IDs preserved, no skin/clothing intersection in idle/walk, city LOD and appearance parity, actual screenshots compared to approved target, profiler budget, license documentation. Do not sign off on procedural art or this importer contract alone. Start with one purchased/approved candidate and validate a representative workwear outfit before mapping the entire catalog; use isolated scratch assets until mapping is complete.
