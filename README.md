# Fruit Merge

Unity ile geliştirilen bir meyve birleştirme (merge) mobil oyunu.

## Gereksinimler

- Unity **6000.0.80f1**
- Android/iOS build desteği (Unity Hub üzerinden ilgili platform modülleri kurulu olmalı)

## Kurulum

1. Bu depoyu klonlayın.
2. Unity Hub üzerinden projeyi açın (Unity `6000.0.80f1` sürümü ile).
3. Unity, gerekli paketleri (`Packages/manifest.json`) otomatik olarak indirecektir.

## Proje Yapısı

```
Assets/FruitMerge/
├── Scripts/
│   ├── Core/       # Oyun döngüsü, genel altyapı
│   ├── Gameplay/   # Birleştirme mekaniği ve oyun içi sistemler
│   ├── UI/         # Arayüz ekranları ve HUD
│   ├── Data/       # Oyun verileri (ScriptableObject'ler vb.)
│   └── Services/   # Servis entegrasyonları
├── Prefabs/
├── Art/
├── Audio/
├── Fonts/
└── Scenes/
```

## Lisans

Bu proje özel (private) bir projedir.
