# Publier AlliedDefenses sur Thunderstore (automatique via GitHub)

Le workflow GitHub publie automatiquement sur Thunderstore **à chaque tag de version**,
en plus de créer la release. Il faut juste une configuration **une seule fois**.

---

## A. Préparer Thunderstore (une fois)

1. Va sur https://thunderstore.io et **connecte-toi** (Discord, GitHub ou Overwolf).
2. Crée une **Team** dont le nom est **exactement** ton namespace : **`Remilulz_91`**.
   - Menu en haut → **Teams** → **Create Team** → nom `Remilulz_91`.
   - ⚠️ Ce nom doit correspondre au `namespace: Remilulz_91` du workflow. Si tu choisis
     un autre nom de team, change aussi le `namespace` dans `.github/workflows/build.yml`.
3. Crée un **Service Account** (= compte robot pour publier) :
   - Team `Remilulz_91` → **Settings** → **Service Accounts** → **Add Service Account**.
   - Tu obtiens un **token** qui commence par `tss_...`.
   - ⚠️ **Copie-le tout de suite, il n'est affiché qu'une fois.** Ne le mets jamais dans le code.

---

## B. Donner le token à GitHub (une fois)

1. Sur ton dépôt GitHub → **Settings** → **Secrets and variables** → **Actions**.
2. **New repository secret** :
   - **Name** : `TS_TOKEN`
   - **Secret** : colle le token `tss_...`
3. **Add secret**.

C'est tout pour la config. GitHub utilisera ce secret pour publier, sans jamais l'exposer.

---

## C. Publier une version

À chaque fois que tu veux sortir une version sur Thunderstore :

1. Mets à jour le **numéro de version** (il doit être **plus grand** que le précédent, et
   identique dans les 3 fichiers) :
   - `manifest.json` → `"version_number"`
   - `AlliedDefenses.csproj` → `<Version>`
   - `src/Plugin.cs` → `Version`
   - (et ajoute une ligne dans `CHANGELOG.md`)
2. Commit + tag + push :
   ```bash
   git add .
   git commit -m "v0.3.0"
   git push
   git tag v0.3.0
   git push origin v0.3.0
   ```
3. GitHub : onglet **Actions** → le build se lance, **compile**, crée la **release**, ET
   **publie sur Thunderstore**. À la fin, ton mod est en ligne sur
   `https://thunderstore.io/c/lethal-company/p/Remilulz_91/AlliedDefenses/`.

Tes potes pourront alors l'installer en **1 clic** depuis r2modman (et BepInEx + les
dépendances s'installeront tout seuls).

---

## D. Dépendances affichées sur Thunderstore

Elles viennent de `manifest.json` → `dependencies`. Actuellement : **BepInExPack**.

Pour le **contrôle manuel à distance**, le mod a besoin d'**OpenBodyCams**. Pour le rendre
obligatoire (installé automatiquement chez les joueurs), ajoute sa chaîne de dépendance
dans `manifest.json`. Récupère la version exacte sur la page Thunderstore d'OpenBodyCams
(format `Zaggy1024-OpenBodyCams-X.Y.Z`), par exemple :

```json
"dependencies": [
  "BepInEx-BepInExPack-5.4.2100",
  "Zaggy1024-OpenBodyCams-3.0.6"
]
```

(Mets le bon numéro de version vu sur la page d'OpenBodyCams.)

---

## E. Si la publication échoue

Regarde l'étape **« Publish to Thunderstore »** rouge dans Actions :

- **« package already exists » / version** : tu n'as pas augmenté le numéro de version.
  Thunderstore refuse de réécrire une version existante.
- **Team / namespace mismatch** : le nom de la team Thunderstore ne correspond pas au
  `namespace` du workflow.
- **Token invalide / manquant** : vérifie le secret `TS_TOKEN`.
- **Version de l'action** : si `GreenTF/upload-thunderstore-package@v4.3` n'existe plus,
  remplace par la dernière version indiquée sur
  https://github.com/marketplace/actions/upload-thunderstore-package

---

## F. Alternative : upload manuel (sans GitHub)

Tu peux aussi publier à la main : récupère `AlliedDefenses.zip` (depuis une release ou les
artifacts), puis sur Thunderstore → ta team → **Upload package** → dépose le zip. Pratique
pour un premier test sans toucher au token.
