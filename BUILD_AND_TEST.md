# AlliedDefenses — Build & Test (procédure pas à pas)

Objectif : compiler le mod en `.dll`, le rendre compatible multijoueur (Netcode Patcher),
l'installer dans Lethal Company et le tester.

> Tu n'as PAS besoin du jeu pour compiler (les DLL du jeu viennent de NuGet). Tu en as
> besoin uniquement pour tester.

---

## 1. Prérequis (à installer une fois)

1. **.NET SDK 8** (ou 6) — https://dotnet.microsoft.com/download
   Vérifie ensuite dans un terminal : `dotnet --version`
2. **Le jeu Lethal Company** (Steam).
3. **r2modman** (gestionnaire de mods) — https://thunderstore.io/c/lethal-company/p/ebkr/r2modman/
   - Crée un **profil de test** dédié (ex. `dev`).
   - Dans ce profil, installe **BepInExPack** (dépendance de base).
   - Lance le jeu une fois via r2modman pour que BepInEx génère ses dossiers.

---

## 2. Configurer la source NuGet de BepInEx (une fois)

Le projet récupère BepInEx depuis un dépôt NuGet séparé. Le `.csproj` le déclare déjà,
mais si le restore échoue, ajoute la source globalement :

```bash
dotnet nuget add source https://nuget.bepinex.dev/v3/index.json --name BepInEx
```

---

## 3. Compiler

Dans un terminal, place-toi dans le dossier du mod puis :

```bash
cd "chemin/vers/AlliedDefenses"
dotnet build -c Release
```

Résultat attendu : `bin/Release/AlliedDefenses.dll`.

- Si erreur sur `LethalCompany.GameLibs.Steam` : ouvre `AlliedDefenses.csproj` et fixe une
  version précise du paquet correspondant à ta version du jeu (voir
  https://www.nuget.org/packages/LethalCompany.GameLibs.Steam — prends la version qui
  correspond à la build actuelle du jeu).

---

## 4. Rendre les RPC multijoueur fonctionnels (Netcode Patcher)

Le mod utilise des RPC personnalisés : ils doivent être « patchés ». Deux options.

### Option A — la plus simple pour tester (patch au runtime) ✅ recommandé pour démarrer

1. Dans ton profil r2modman, installe le mod **Runtime Netcode Patcher** (par Ozone) :
   https://thunderstore.io/c/lethal-company/p/Ozone/Runtime_Netcode_Patcher/
2. C'est tout : il patche les assemblies au lancement. Tu compiles normalement (étape 3),
   aucune manipulation supplémentaire du `.dll`.
3. Pour la publication, ajoute-le dans les dépendances de `manifest.json`.

### Option B — patch à la compilation (pour une version « propre » à distribuer)

1. Installe l'outil une fois :
   ```bash
   dotnet tool install -g Evaisa.NetcodePatcher.Cli
   ```
2. Patche le `.dll` après chaque build :
   ```bash
   netcode-patch "bin/Release/AlliedDefenses.dll" "<dossiers des DLL Unity/Netcode>"
   ```
   (Détails et intégration MSBuild automatique : https://github.com/EvaisaDev/UnityNetcodePatcher)
3. Le `.csproj` est déjà réglé en `DebugType=portable` (obligatoire, sinon l'outil échoue).

> Pour un premier test, prends l'**Option A**.

---

## 5. Installer le mod

1. Repère le dossier plugins de ton profil de test, du type :
   `...\r2modman\LethalCompany\profiles\dev\BepInEx\plugins\`
2. Crée un sous-dossier `AlliedDefenses` et copie dedans `AlliedDefenses.dll`
   (et plus tard `manifest.json` + `icon.png` + `README.md` pour un vrai package).
3. (Astuce dev : tu peux automatiser la copie avec un post-build, ou utiliser
   l'option « importer un mod local » de r2modman.)

---

## 6. Lancer et vérifier le chargement

1. Lance le jeu **via r2modman** (profil de test).
2. La console BepInEx doit afficher :
   ```
   AlliedDefenses v0.1.0 by Remilulz_91 loaded. 2 defense module(s) active.
   ```
   Si tu ne vois pas la console : dans r2modman, active le log/console BepInEx,
   ou regarde `BepInEx/LogOutput.log`.

---

## 7. Tester en jeu (checklist)

Lance une partie, descends dans l'usine, puis à l'ordinateur du vaisseau (ou en multi,
demande à l'opérateur terminal) :

- [ ] `ally help` → affiche l'explication du mod.
- [ ] `ally config` → affiche durée, portée, friendly fire, couleurs.
- [ ] `ally <id>` (ex. `ally U9`) → la défense devient alliée.
      - Tourelle : vise/tire les ennemis, plus les joueurs ; **laser vert** en jeu.
      - Mine : n'explose plus sous les joueurs, déclenche sur les ennemis.
      - Sur la carte radar : le code passe en **bleu**, avec le timer `m:ss`.
- [ ] `ally turrets` / `ally mines` → bascule tout le groupe.
- [ ] `ally control <id>` → prise de contrôle : le moniteur passe en gun-cam,
      souris pour viser, **clic gauche** pour tirer (touche tout), **V** ou
      `ally release` pour rendre la main.
- [ ] Vérifie qu'à l'expiration du timer, la défense redevient hostile et le code
      reprend sa couleur normale.
- [ ] Vérifie que taper juste l'id (`U9`) garde le comportement vanilla (désactivation 2 s).

---

## 8. Tester en multijoueur

- Tous les joueurs doivent avoir **le mod** (même version) et, si tu as pris l'Option A,
  **Runtime Netcode Patcher**.
- Hôte + 1 client minimum. Vérifie que le piratage, la couleur, le timer et le pilotage
  manuel sont **identiques chez tout le monde**.

---

## 9. Problèmes fréquents

| Symptôme | Cause probable | Solution |
|---|---|---|
| Le mod ne se charge pas | BepInEx absent / mauvais dossier | Vérifie l'install BepInEx et le chemin `plugins/`. |
| Erreur build sur GameLibs | Version du paquet ≠ version du jeu | Fixe la version dans le `.csproj`. |
| Piratage non synchronisé en multi | Netcode Patcher manquant | Option A (Runtime Netcode Patcher) ou Option B. |
| `netcode-patch` erreur illisible | Symboles de debug « full » | Déjà réglé : `DebugType=portable`. |
| Commande terminal sans effet | Mauvais mot-clé | Le mot-clé est `ally` (configurable dans le `.cfg`). |
| Noms de membres du jeu (laser, DamagePlayer…) | Renommés par une MAJ | Voir « Confirmed game API » du README et ajuster. |

---

## 10. Publier sur Thunderstore (plus tard)

Zippe à la racine : `AlliedDefenses.dll` (patché si Option B), `manifest.json`, `icon.png`,
`README.md`. Vérifie le `manifest.json` (nom, version, dépendances : BepInExPack + le
Runtime Netcode Patcher si Option A), puis upload sur Thunderstore.
