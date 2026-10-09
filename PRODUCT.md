# TournéeVéto — Contexte produit

> **Avertissement :** toutes les données (élevages, vaches, personnes, résultats) sont **fictives**. Les règles métier sont **simplifiées** et ne remplacent ni le jugement clinique, ni la réglementation, ni les protocoles d'une clinique. Ce document sert de contexte permanent aux agents de développement.

**Cible :** Québec, interface en français uniquement. **Appareil principal :** tablette en étable (tactile, gants possibles, une main). **Plateforme du POC :** PWA 100 % navigateur sur GitHub Pages, sans backend, sans compte, données stockées localement, hors ligne. Les écrans seront réutilisés en WPF (Windows) et MAUI (mobile) : logique métier séparée de l'interface.

## 1. Problème

- Le vétérinaire en production laitière prépare ses tournées et rédige ses rapports avec des outils dispersés (papier, tableurs, logiciel de clinique), souvent après la visite, à partir de notes.
- Les étables ont une couverture réseau faible ou nulle : les outils infonuagiques y sont peu utilisables.
- Ordre de grandeur : le Québec compte environ 4 500 à 5 000 fermes laitières (**à vérifier** — source à confirmer : Producteurs de lait du Québec / Statistique Canada). Le temps de rédaction par visite est une hypothèse à valider par entrevues (**à vérifier**).

## 2. Personas

**Dre Marie Tremblay — vétérinaire de clinique de groupe (principale)**
- Objectif : arriver préparée, consigner les actions sur les vaches pendant la visite, remettre un rapport avant de quitter ou le soir même.
- Frustration : ressaisie des notes, oubli des suivis de la visite précédente.
- Contexte : 5 à 8 visites par jour, véhicule puis étable, tablette, hors ligne, mains souvent sales.

**Luc Gagnon — producteur laitier (destinataire du rapport)**
- Objectif : savoir quoi faire, sur quelles vaches, et avant quand.
- Frustration : rapports longs, tardifs ou peu actionnables.
- Contexte : lit le rapport sur téléphone ou imprimé ; n'utilise pas l'application dans le POC.

**Sophie Roy — coordonnatrice de clinique (secondaire)**
- Objectif : voir la tournée du jour de chaque vétérinaire et retrouver les rapports.
- Frustration : informations éparpillées entre agendas et dossiers.
- Contexte : bureau, poste Windows (futur WPF). Hors périmètre du POC, à garder en tête pour l'architecture.

## 3. Proposition de valeur

TournéeVéto permet au vétérinaire de préparer sa tournée, saisir constats et actions d'élevage hors ligne, et produire le rapport de visite sur place.

**Parcours clés**
1. **Préparer la tournée du jour :** consulter les élevages du jour, ouvrir une fiche élevage (effectif, dernières visites, actions en suspens).
2. **Réaliser la visite avec la grille de régie :** parcourir les vaches à traiter (vêlage, tarissement, insémination, diagnostic de gestation, CCS élevée), cocher les actions réalisées, ajouter des notes.
3. **Clore la visite :** remplir le bilan de biosécurité, générer le rapport (résumé, actions faites, actions à venir, biosécurité), l'imprimer ou l'exporter en PDF.

## 4. Hors périmètre du POC

- Backend, synchronisation, comptes, authentification, multi-utilisateur.
- Intégrations (contrôle laitier, logiciels de clinique, facturation, agenda).
- Import de fichiers, lecture d'identifiants électroniques ou de codes-barres.
- Prescription, inventaire de médicaments, registre officiel de traitements.
- Règles métier complètes ou réglementaires ; calculs génétiques ou économiques.
- Interface en anglais.
- Données réelles ou confidentielles.
- Applications WPF et MAUI (seulement préparées par la séparation des couches).

## 5. Glossaire

- **Régie :** ensemble des pratiques de conduite du troupeau (reproduction, alimentation, santé, logement). La *grille de régie* liste les actions à faire sur les vaches.
- **Vêlage :** mise bas d'une vache ; marque le début d'une lactation.
- **Tarissement :** arrêt de la traite avant le vêlage suivant, pour une période de repos (environ 60 jours, valeur simplifiée).
- **Insémination :** dépôt de semence dans l'utérus pour provoquer une gestation (insémination artificielle).
- **Diagnostic de gestation :** examen (palpation, échographie, test) confirmant ou non la gestation, à un délai donné après l'insémination.
- **CCS (comptage de cellules somatiques) :** nombre de cellules par mL de lait ; indicateur de santé de la mamelle (mammite). Un seuil élevé déclenche une action (seuil simplifié dans le POC).
- **Biosécurité :** mesures limitant l'entrée et la propagation des maladies dans l'élevage (visiteurs, animaux introduits, quarantaine, nettoyage). Le *bilan* du POC comporte quatre thèmes simplifiés — visiteurs, introductions d'animaux, isolement et équarrissage — et ne constitue ni une certification ni un avis clinique.
- **Élevage :** exploitation laitière suivie par le vétérinaire ; unité de la fiche (producteur, troupeau, adresse fictive).
- **Visite :** passage du vétérinaire dans un élevage à une date donnée ; produit une grille de régie remplie, un bilan de biosécurité et un rapport.

## 6. Jeu de démonstration local (S02)

- Première initialisation : 5 élevages fictifs, 100 vaches par élevage et 3 visites par élevage
  (30 jours avant la date de référence, le jour de référence et le lendemain).
- Chaque visite du jour contient un exemple de chacune des 5 catégories d'actions ; chaque
  visite précédente contient une action fictive réalisée, soit 15 visites et 30 actions.
  Ces associations servent de fixtures de démonstration : elles ne sont pas des conclusions
  cliniques calculées à partir des vaches et ne remplacent pas les futures règles de régie.
- La date de référence vient du `TimeProvider` injecté au premier démarrage ; la graine vaut 42.
  Le générateur complet accepte les dates du 0010-01-01 (année 10) au 9999-12-30,
  afin de permettre les dates historiques des vaches et la visite du lendemain.
- L'initialisation IndexedDB est atomique. Les ouvertures suivantes relisent les données
  locales sans changer les dates de référence, restaurer les visites supprimées ni écraser
  les notes, actions ou photos modifiées.
- Un troupeau déjà enregistré par l'ancien socle est adopté tel quel, sans enrichissement
  automatique de l'historique. Des données illisibles provoquent une erreur, pas une remise à zéro.
- Le stockage est limité à cet appareil, sans sauvegarde ni synchronisation. Son effacement
  et la fermeture d'une session privée peuvent supprimer les saisies. Sa disponibilité ne
  prouve pas que les ressources de l'application sont en cache pour un redémarrage hors ligne.
