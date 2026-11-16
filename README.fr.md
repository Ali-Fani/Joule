<h1 align="center"><img src="docs/images/capa.png" alt="Joule" width="100%"></h1>

<p align="center"><strong>La batterie de votre souris, casque, manette et clavier sans fil, dans la zone de notification de Windows.</strong></p>

<p align="center">
  <a href="https://github.com/DillanCidral/Joule/releases/latest"><img src="https://img.shields.io/github/v/release/DillanCidral/Joule?label=version&color=FFC21F&labelColor=1D1B18" alt="Dernière version"></a>
  <a href="https://github.com/DillanCidral/Joule/releases"><img src="https://img.shields.io/github/downloads/DillanCidral/Joule/total?label=t%C3%A9l%C3%A9chargements&color=FFC21F&labelColor=1D1B18" alt="Téléchargements"></a>
  <img src="https://img.shields.io/badge/Windows-11-FFC21F?labelColor=1D1B18" alt="Windows 11">
  <a href="LICENSE"><img src="https://img.shields.io/badge/licence-AGPL--3.0-FFC21F?labelColor=1D1B18" alt="Licence : AGPL-3.0"></a>
</p>

<p align="center"><img src="docs/images/bandeira-fr.png" alt="" height="14"> <strong>Français</strong> · <a href="README.md"><img src="docs/images/bandeira-us.png" alt="" height="14"> English</a> · <a href="README.pt-BR.md"><img src="docs/images/bandeira-br.png" alt="" height="14"> Português</a></p>

<p align="center">
  <a href="https://github.com/DillanCidral/Joule/releases/latest/download/JouleApp-win-Setup.exe"><img src="https://img.shields.io/badge/T%C3%A9l%C3%A9charger_pour_Windows-FFC21F?style=for-the-badge" alt="Télécharger pour Windows" height="40"></a>
</p>

<p align="center"><img src="docs/images/joule.png" alt="Panneau et widget de bord de Joule, thème sombre" width="85%"></p>

Joule est une petite application Windows 11 qui reste dans la zone de
notification et indique la batterie restante de vos appareils sans fil. Elle
est gratuite, open source et en **bêta**.

## Ce que vous obtenez

- **Panneau** : cliquez sur l'icône pour voir tous les appareils connectés, le
  plus faible en premier.
- **Widget de bord** : des anneaux de batterie sur le bord de l'écran. Il se
  masque tout seul et s'efface devant les applications en plein écran.
- **Icône** : montre d'un coup d'œil si une batterie est faible ; survolez-la
  pour voir la batterie de chaque appareil.
- **Avis de connexion** : quand un appareil s'allume, Joule affiche sa
  batterie, sur le widget ou en notification Windows, avec un son si vous le
  souhaitez.
- **Alerte de batterie faible**, avec un seuil que vous choisissez.
- **Français, anglais et portugais** : suit la langue de Windows, et vous
  pouvez en changer dans les Réglages.
- **Résumé après mise à jour** : combien d'appareils Joule a suivis et combien
  d'alertes de batterie faible il a données, compté uniquement sur votre PC,
  ainsi que les nouveautés de la version.

## Téléchargement

Récupérez `JouleApp-win-Setup.exe` avec le bouton ci-dessus ou depuis la
[page Releases](https://github.com/DillanCidral/Joule/releases), puis
lancez-le.

- Aucun droit administrateur requis. Il s'installe pour votre utilisateur
  uniquement et apparaît dans *Applications installées*, d'où vous pouvez le
  supprimer.
- L'installeur n'est pas signé numériquement, Windows SmartScreen affichera
  donc un avertissement. Cliquez sur **Informations complémentaires**, puis
  **Exécuter quand même**.
- Joule se met à jour depuis GitHub Releases et vous prévient dans la zone de
  notification quand une nouvelle version est prête.

## Appareils pris en charge

| Appareil | Connexion | État |
|---|---|---|
| Attack Shark R1 | dongle | ✅ vérifié |
| Rapoo MT760 Mini | dongle | ✅ vérifié |
| DualSense | câble | ✅ vérifié |
| Manettes Xbox | adaptateur et câble | ✅ vérifié |
| 8BitDo Ultimate 2C | câble | ✅ vérifié |
| Tout appareil Bluetooth dont Windows affiche déjà la batterie dans *Paramètres > Bluetooth* | Bluetooth | ✅ vérifié |
| Logitech, Razer, SteelSeries, HyperX et Corsair : souris, claviers et casques | dongle | 🧪 pas encore testé |
| Une vingtaine d'autres marques : ATK, VXE, VGN, Pulsar, Dareu, AJAZZ, Lamzu, WLmouse, G-Wolves, Hitscan, K-Snake, Bytech, Motospeed, Keychron, Incott et d'autres modèles Attack Shark | dongle | 🧪 pas encore testé |

🧪 signifie écrit d'après une documentation de protocole ouverte et pas encore
testé sur du matériel réel. Si vous en possédez un, c'est votre retour qui le
confirme. Liste complète (en anglais) :
[docs/supported-devices.md](docs/supported-devices.md).

## Mon appareil n'est pas dans la liste

Ouvrez **Réglages > Envoyer mon appareil** dans Joule. Il écoute pendant
30 secondes pendant que vous utilisez l'appareil, vous montre ce qu'il a
trouvé, retire les données personnelles (numéro de série, adresse Bluetooth,
nom du PC, nom d'utilisateur) et enregistre un `.txt`. Vous envoyez ensuite ce
fichier vous-même, sous forme d'
[issue GitHub](https://github.com/DillanCidral/Joule/issues/new/choose) ou par
[e-mail](#contact). Rien ne quitte votre PC tout seul.

Vous voulez ajouter l'appareil vous-même ? Voir
[docs/adding-a-device.md](docs/adding-a-device.md) (en anglais).

<details>
<summary><strong>Ce que Joule fait à vos appareils</strong></summary>

- Il lit à l'ouverture de l'application, à l'ouverture de session Windows,
  quand vous cliquez sur **Actualiser** et quand vous branchez ou allumez un
  appareil (uniquement cet appareil, au plus une fois toutes les 10 secondes).
  Il n'interroge jamais en boucle.
- Pour la plupart des appareils, il se contente d'**écouter** ce que le dongle
  ou le câble envoie déjà, ou de lire le chiffre que Windows a lui-même
  enregistré (Bluetooth).
- Pour certaines marques sur dongle (Logitech, Razer, SteelSeries, HyperX,
  Corsair et une partie des autres), il **demande** sa batterie à l'appareil,
  uniquement après que vous avez autorisé cette marque : la même question que
  celle envoyée par des projets ouverts comme Solaar, OpenRazer et
  HeadsetControl, une fois par lecture. Il n'envoie jamais de commande qui
  modifie un réglage. L'interrupteur **Écoute seule** des Réglages désactive
  toutes les questions.
- Il n'affiche jamais un chiffre qu'il n'a pas lu. Pas de données, c'est
  « pas de données », jamais « 0 % ».
- Il n'envoie rien nulle part. Le seul accès réseau est la recherche de mise à
  jour sur GitHub Releases.

Détails (en anglais) : [SECURITY.md](SECURITY.md).

</details>

<details>
<summary><strong>Compiler depuis les sources</strong></summary>

Il vous faut Windows et le [SDK .NET 8](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
dotnet build src/BatteryTray -c Debug
```

```bash
dotnet run --project src/BatteryTray -- --demo
```

`--demo` affiche des appareils d'exemple sans toucher au matériel. Plus
d'informations (en anglais) dans [CONTRIBUTING.md](CONTRIBUTING.md) et
[docs/architecture.md](docs/architecture.md).

</details>

## Soutenir Joule

Joule est gratuit et open source. S'il vous est utile, un café aide à ce que
ça continue : [soutenir Joule](https://buy.stripe.com/bJeeVe5yq5Pub7bfz8bZe00).

## Contact

- E-mail : dillan.apps@gmail.com
- Instagram : [@dillan.apps](https://www.instagram.com/dillan.apps/)
- YouTube : [@dillanapps](https://www.youtube.com/@dillanapps)

## Licence

[AGPL-3.0](LICENSE). Les licences tierces et les projets ouverts dont la
documentation de protocole a rendu Joule possible sont listés dans
[THIRD-PARTY-NOTICES.md](THIRD-PARTY-NOTICES.md).

Joule n'est affilié ni à Logitech, Razer, SteelSeries, HyperX, Corsair, Sony,
Microsoft, 8BitDo, ni à aucune autre marque mentionnée ici. Leurs noms et
marques appartiennent à leurs propriétaires.
