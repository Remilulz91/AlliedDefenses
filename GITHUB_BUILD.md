# Compiler le mod automatiquement avec GitHub (sans rien installer)

Le dépôt contient un workflow GitHub Actions (`.github/workflows/build.yml`) qui, à chaque
push, **compile le mod, patche le netcode, et te fournit le `.dll` prêt à glisser-déposer**.
Tu n'as donc pas besoin de .NET sur ta machine.

---

## A. Mettre le code sur GitHub (une fois)

1. Crée un compte sur https://github.com si ce n'est pas fait, et installe **Git** :
   https://git-scm.com/downloads
2. Sur GitHub, clique **New repository** → nomme-le `AlliedDefenses` → **Create**
   (ne coche rien, pas de README).
3. Ouvre un terminal **dans le dossier du mod** (celui qui contient `AlliedDefenses.csproj`) :

```bash
cd "chemin/vers/AlliedDefenses"
git init
git add .
git commit -m "AlliedDefenses initial"
git branch -M main
git remote add origin https://github.com/Remilulz91/AlliedDefenses.git
git push -u origin main
```

(Pseudo GitHub : `Remilulz91`. La team Thunderstore, elle, reste `Remilulz_91`.)

---

## B. Récupérer le `.dll` compilé

Dès que tu pushes, GitHub lance le build automatiquement.

1. Sur ton dépôt GitHub → onglet **Actions**.
2. Clique sur le dernier run **« Build AlliedDefenses »** (point vert = succès).
3. En bas, section **Artifacts** → télécharge **`AlliedDefenses`**.
4. Dézippe : tu obtiens `AlliedDefenses.dll` (+ `manifest.json`, `icon.png`, `README.md`).

Le `.dll` est déjà **netcode-patché** : tu peux le glisser directement dans
`...\BepInEx\plugins\AlliedDefenses\` et jouer (voir `BUILD_AND_TEST.md`, étapes 5-7).

---

## C. (Pratique) Créer une vraie « Release » téléchargeable

Pour avoir un lien de téléchargement direct (et le zip Thunderstore) :

```bash
git tag v0.1.0
git push --tags
```

GitHub crée alors une **Release** avec `AlliedDefenses.dll` et `AlliedDefenses.zip`
attachés (onglet **Releases** du dépôt). Idéal pour partager le mod ou l'uploader sur
Thunderstore.

Pour les versions suivantes : change `version_number` dans `manifest.json` et `Version`
dans `AlliedDefenses.csproj`, puis crée un nouveau tag (`v0.2.0`, etc.).

---

## D. Mettre à jour le mod ensuite

À chaque modification :

```bash
git add .
git commit -m "ce que tu as changé"
git push
```

→ un nouveau build se lance, tu retournes dans **Actions** récupérer le `.dll`.

---

## Si le build échoue (point rouge)

Ouvre le run dans **Actions** et regarde quelle étape est rouge :

- **Étape « Restore » rouge** : souvent une version de `LethalCompany.GameLibs.Steam`
  indisponible. Dans `AlliedDefenses.csproj`, fixe une version précise correspondant à ta
  version du jeu (voir https://www.nuget.org/packages/LethalCompany.GameLibs.Steam).

- **Étape « Build … netcode-patched » rouge** : les versions Unity/Netcode ne correspondent
  pas à la version actuelle du jeu. Deux solutions :
  1. Ajuste les valeurs dans `AlliedDefenses.csproj` (`NcpUnityVersion`,
     `NcpNetcodeVersion`, `NcpTransportVersion`).
  2. **Plan B simple** : enlève `-p:NetcodePatch=true` dans `.github/workflows/build.yml`
     (le `.dll` ne sera pas patché), puis en jeu installe le mod **Runtime Netcode Patcher**
     (par Ozone) qui patche au lancement. C'est la voie la plus tolérante.

Dans tous les cas, copie-moi le message d'erreur de l'étape rouge et je te dis quoi changer.
