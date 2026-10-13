using FitnessMerchant.Api.Domain.Catalog;
using static FitnessMerchant.Api.Infrastructure.Seed.ArtworkKind;

namespace FitnessMerchant.Api.Infrastructure.Seed;

public sealed record CategorySeed(string Slug, string Name, Department Department, int SortOrder, string Description);

public sealed record BrandSeed(string Slug, string Name, string SkuPrefix, string AccentColor, string Description);

/// <param name="AddedDaysAgo">Spreads <c>CreatedAt</c> so "new arrivals" has a meaningful order.</param>
public sealed record ProductSeed(
    string Name,
    string Category,
    string Brand,
    decimal Price,
    int Discount,
    int Stock,
    ArtworkSpec Art,
    int AddedDaysAgo,
    string Description);

/// <summary>
/// The fictional catalog (D002). Every brand and product name is invented for this project;
/// a unit test guards against real brand names creeping in.
/// </summary>
public static class CatalogSeedData
{
    public static readonly IReadOnlyList<CategorySeed> Categories =
    [
        new("protein", "Protein", Department.Supplements, 1, "Whey, casein and plant proteins for recovery and daily intake."),
        new("pre-workout", "Pre-Workout", Department.Supplements, 2, "Energy, focus and pump formulas for training days."),
        new("creatine-amino-acids", "Creatine & Amino Acids", Department.Supplements, 3, "Creatine, BCAAs, EAAs and single amino acids."),
        new("vitamins-health", "Vitamins & Health", Department.Supplements, 4, "Everyday vitamins, minerals and wellness support."),
        new("strength-equipment", "Strength Equipment", Department.Gear, 1, "Barbells, dumbbells, kettlebells, plates and benches."),
        new("cardio-equipment", "Cardio Equipment", Department.Gear, 2, "Treadmills, bikes, rowers and cardio accessories."),
        new("apparel", "Apparel", Department.Gear, 3, "Training tops, hoodies and shorts."),
        new("accessories", "Accessories", Department.Gear, 4, "Shakers, belts, bands, mats and bags."),
    ];

    public static readonly IReadOnlyList<BrandSeed> Brands =
    [
        new("halvard-nutrition", "Halvard Nutrition", "HAL", "#e4572e",
            "Straightforward sports nutrition: proteins, creatine and essentials with short ingredient lists."),
        new("quillon-labs", "Quillon Labs", "QUI", "#2e86ab",
            "Performance formulas built around clinically studied doses, made for hard training blocks."),
        new("verdance-fuel", "Verdance Fuel", "VER", "#4f9a3a",
            "Plant-forward nutrition and everyday wellness, with natural flavours and no artificial colours."),
        new("brakken-barbell-co", "Brakken Barbell Co.", "BRK", "#7b4fa0",
            "Strength equipment for garage gyms and training halls: bars, plates, bells and benches."),
        new("tidewell-athletics", "Tidewell Athletics", "TID", "#d1495b",
            "Training apparel and accessories designed to move, breathe and last."),
        new("orrin-cardio", "Orrin Cardio", "ORR", "#1f8a8a",
            "Quiet, sturdy cardio machines and conditioning tools for home and studio."),
    ];

    public static readonly IReadOnlyList<ProductSeed> Products =
    [
        // Protein
        new("Whey Isolate Vanilla 2 kg", "protein", "halvard-nutrition", 59.90m, 10, 42, new(Tub, "WHEY", "VANILLA 2 KG"), 120,
            "Fast-digesting whey isolate with 27 g protein per serving and a smooth vanilla taste."),
        new("Whey Isolate Chocolate 2 kg", "protein", "halvard-nutrition", 59.90m, 0, 37, new(Tub, "WHEY", "CHOCOLATE 2 KG"), 118,
            "The same clean isolate in a rich cocoa flavour that mixes easily with water or milk."),
        new("Casein Night Protein 1 kg", "protein", "halvard-nutrition", 34.90m, 0, 4, new(Tub, "CASEIN", "VANILLA 1 KG"), 75,
            "Slow-release micellar casein for a steady protein supply overnight."),
        new("Plant Protein Blend Cocoa 1 kg", "protein", "verdance-fuel", 32.50m, 15, 25, new(Tub, "PLANT", "COCOA 1 KG"), 60,
            "Pea, rice and pumpkin protein with a complete amino acid profile. Vegan."),
        new("Pea and Rice Protein Unflavoured 1 kg", "protein", "verdance-fuel", 27.90m, 0, 0, new(Tub, "PLANT", "UNFLAVOURED"), 200,
            "A neutral plant protein for smoothies and baking."),
        new("Mass Gainer Banana 3 kg", "protein", "quillon-labs", 44.90m, 20, 18, new(Tub, "GAINER", "BANANA 3 KG"), 30,
            "High-calorie blend of oats, whey and healthy fats for lean bulking phases."),
        new("Clear Whey Lemon 500 g", "protein", "quillon-labs", 29.90m, 0, 12, new(Jar, "CLEAR", "LEMON 500 G"), 8,
            "A light, juice-like whey isolate drink for hot days and post-cardio."),
        new("Collagen Peptides 450 g", "protein", "verdance-fuel", 24.90m, 0, 30, new(Jar, "COLLAGEN", "450 G"), 45,
            "Hydrolysed collagen peptides that dissolve in coffee, tea or water."),

        // Pre-workout
        new("Ignite Pre-Workout Berry 300 g", "pre-workout", "quillon-labs", 31.90m, 0, 26, new(Jar, "IGNITE", "BERRY 300 G"), 14,
            "Caffeine, citrulline and beta-alanine for focused, high-output sessions."),
        new("Ignite Pre-Workout Mango 300 g", "pre-workout", "quillon-labs", 31.90m, 25, 3, new(Jar, "IGNITE", "MANGO 300 G"), 90,
            "The Ignite formula in a bright mango flavour."),
        new("Stim-Free Pump Formula 350 g", "pre-workout", "halvard-nutrition", 28.50m, 0, 20, new(Jar, "PUMP", "STIM-FREE"), 50,
            "Nitric oxide support without caffeine, for evening training."),
        new("Natural Energy Pre-Workout 250 g", "pre-workout", "verdance-fuel", 26.90m, 10, 15, new(Jar, "ENERGY", "NATURAL"), 35,
            "Green tea caffeine and beetroot extract, naturally flavoured."),
        new("Caffeine Tablets 200 mg x 100", "pre-workout", "quillon-labs", 9.90m, 0, 64, new(Bottle, "CAFFEINE", "200 MG"), 150,
            "Simple, precisely dosed caffeine tablets."),
        new("Beta-Alanine Powder 300 g", "pre-workout", "halvard-nutrition", 17.90m, 0, 0, new(Jar, "BETA", "ALANINE"), 210,
            "Pure beta-alanine to support high-rep and interval work."),
        new("Electrolyte Hydration Mix 30 Servings", "pre-workout", "verdance-fuel", 19.90m, 30, 40, new(Jar, "HYDRATE", "30 SERVINGS"), 5,
            "Sodium, potassium and magnesium in a low-sugar citrus drink."),

        // Creatine & amino acids
        new("Creatine Monohydrate 500 g", "creatine-amino-acids", "halvard-nutrition", 24.90m, 0, 80, new(Jar, "CREATINE", "500 G"), 180,
            "Micronised creatine monohydrate, 5 g per serving, unflavoured."),
        new("Creatine Monohydrate 1 kg", "creatine-amino-acids", "halvard-nutrition", 42.90m, 15, 22, new(Tub, "CREATINE", "1 KG"), 178,
            "The value size of our pure creatine monohydrate."),
        new("Creatine Capsules x 240", "creatine-amino-acids", "quillon-labs", 22.50m, 0, 5, new(Bottle, "CREATINE", "240 CAPS"), 70,
            "Creatine in easy-to-dose capsules for travel and busy days."),
        new("BCAA 2:1:1 Watermelon 400 g", "creatine-amino-acids", "quillon-labs", 23.90m, 0, 33, new(Jar, "BCAA", "WATERMELON"), 95,
            "Branched-chain amino acids in the classic 2:1:1 ratio."),
        new("EAA Recovery Peach 390 g", "creatine-amino-acids", "verdance-fuel", 29.90m, 20, 14, new(Jar, "EAA", "PEACH"), 20,
            "All nine essential amino acids for intra- and post-workout recovery."),
        new("L-Glutamine Powder 500 g", "creatine-amino-acids", "halvard-nutrition", 19.90m, 0, 27, new(Jar, "GLUTAMINE", "500 G"), 130,
            "Pure L-glutamine, unflavoured, easy to add to any shake."),
        new("Citrulline Malate 300 g", "creatine-amino-acids", "quillon-labs", 21.90m, 0, 2, new(Jar, "CITRULLINE", "300 G"), 40,
            "Citrulline malate 2:1 for training volume and pump."),

        // Vitamins & health
        new("Daily Multivitamin x 120", "vitamins-health", "verdance-fuel", 16.90m, 0, 55, new(Bottle, "MULTI", "120 TABS"), 160,
            "A complete daily multivitamin and mineral formula."),
        new("Vitamin D3 and K2 x 90", "vitamins-health", "verdance-fuel", 12.90m, 0, 70, new(Bottle, "D3 + K2", "90 CAPS"), 110,
            "Vitamin D3 with K2 for bones and immune support."),
        new("Omega-3 Fish Oil x 180", "vitamins-health", "halvard-nutrition", 21.90m, 10, 38, new(Bottle, "OMEGA-3", "180 CAPS"), 85,
            "High-strength EPA and DHA from responsibly sourced fish oil."),
        new("Magnesium Bisglycinate x 120", "vitamins-health", "verdance-fuel", 18.50m, 0, 0, new(Bottle, "MAGNESIUM", "120 CAPS"), 140,
            "A gentle, well-absorbed form of magnesium for evenings."),
        new("Zinc and Copper x 100", "vitamins-health", "quillon-labs", 9.50m, 0, 4, new(Bottle, "ZINC", "100 TABS"), 65,
            "Balanced zinc and copper for everyday support."),
        new("Joint Support Complex x 120", "vitamins-health", "halvard-nutrition", 27.90m, 25, 19, new(Bottle, "JOINT", "120 CAPS"), 25,
            "Glucosamine, chondroitin and MSM for hard-working joints."),
        new("Ashwagandha Extract x 60", "vitamins-health", "verdance-fuel", 19.90m, 0, 23, new(Bottle, "CALM", "60 CAPS"), 12,
            "A standardised ashwagandha root extract."),
        new("Greens Superfood Powder 300 g", "vitamins-health", "verdance-fuel", 34.90m, 0, 16, new(Jar, "GREENS", "300 G"), 3,
            "Spirulina, wheatgrass and greens in a mild apple flavour."),

        // Strength equipment
        new("Olympic Barbell 20 kg", "strength-equipment", "brakken-barbell-co", 289.00m, 0, 9, new(Barbell), 220,
            "A 2.2 m, 20 kg bar with medium knurling and smooth-spinning sleeves."),
        new("Adjustable Dumbbell Pair 2 to 24 kg", "strength-equipment", "brakken-barbell-co", 399.00m, 10, 6, new(Dumbbell), 100,
            "Dial-adjustable dumbbells that replace fifteen pairs in one compact set."),
        new("Hex Dumbbell 10 kg", "strength-equipment", "brakken-barbell-co", 39.90m, 0, 30, new(Dumbbell, Fill: "#3f3f46"), 160,
            "Rubber-coated hex dumbbell with a knurled chrome handle. Sold individually."),
        new("Competition Kettlebell 16 kg", "strength-equipment", "brakken-barbell-co", 69.90m, 0, 14, new(Kettlebell, "16", "KG"), 80,
            "Steel competition kettlebell with uniform size across weights."),
        new("Competition Kettlebell 24 kg", "strength-equipment", "brakken-barbell-co", 94.90m, 15, 3, new(Kettlebell, "24", "KG"), 78,
            "The 24 kg bell for swings, cleans and snatches."),
        new("Bumper Plate 20 kg", "strength-equipment", "brakken-barbell-co", 109.00m, 0, 0, new(Plate, "20 KG", "BUMPER"), 190,
            "Dense rubber bumper plate with a steel insert. Sold individually."),
        new("Fractional Plate Set 0.25 to 1.25 kg", "strength-equipment", "brakken-barbell-co", 59.00m, 0, 11, new(Plate, "FRACTIONAL", "SET"), 22,
            "Small-increment steel plates for steady progress on the big lifts."),
        new("Adjustable Weight Bench", "strength-equipment", "brakken-barbell-co", 249.00m, 20, 7, new(Bench), 55,
            "Flat to 85 degrees with a 300 kg load rating and wheels for easy moving."),

        // Cardio equipment
        new("Folding Treadmill T5", "cardio-equipment", "orrin-cardio", 899.00m, 15, 4, new(Treadmill), 140,
            "A space-saving treadmill with 16 km/h top speed and soft deck cushioning."),
        new("Commercial Treadmill T9", "cardio-equipment", "orrin-cardio", 2499.00m, 0, 2, new(Treadmill, Fill: "#0f5f5f"), 300,
            "Studio-grade treadmill with a 22 km/h top speed and a wide running belt."),
        new("Air Bike A3", "cardio-equipment", "orrin-cardio", 749.00m, 0, 5, new(Bike), 65,
            "Fan-resistance bike for intervals: the harder you push, the harder it gets."),
        new("Magnetic Spin Bike S2", "cardio-equipment", "orrin-cardio", 529.00m, 20, 0, new(Bike, Fill: "#14b8a6"), 210,
            "Quiet magnetic resistance with micro-adjustable seat and bars."),
        new("Rowing Machine R7", "cardio-equipment", "orrin-cardio", 1149.00m, 10, 3, new(Rower), 28,
            "Air and magnetic resistance rower with a performance monitor."),
        new("Speed Jump Rope", "cardio-equipment", "orrin-cardio", 14.90m, 0, 90, new(Band, "SPEED", "ROPE"), 150,
            "Ball-bearing speed rope with an adjustable steel cable."),
        new("Heart Rate Chest Strap", "cardio-equipment", "orrin-cardio", 49.90m, 0, 21, new(Band, "HR", "STRAP", "#3f3f46"), 9,
            "Accurate chest-strap heart rate monitor with dual wireless connectivity."),

        // Apparel
        new("Training Tee Black", "apparel", "tidewell-athletics", 24.90m, 0, 60, new(Shirt, Fill: "#27272a"), 40,
            "Lightweight, quick-drying training tee with flat seams."),
        new("Training Tee Sand", "apparel", "tidewell-athletics", 24.90m, 30, 28, new(Shirt, Fill: "#d6c7a1"), 160,
            "The training tee in a warm sand colour."),
        new("Performance Hoodie Charcoal", "apparel", "tidewell-athletics", 59.90m, 0, 17, new(Hoodie, Fill: "#3f3f46"), 18,
            "Brushed-back performance hoodie for warm-ups and cool-downs."),
        new("Track Jacket Navy", "apparel", "tidewell-athletics", 69.90m, 20, 9, new(Hoodie, Fill: "#1e3a5f"), 33,
            "Stretch-woven track jacket with zipped pockets."),
        new("Lightweight Running Shorts", "apparel", "tidewell-athletics", 29.90m, 0, 35, new(Shorts, Fill: "#27272a"), 75,
            "Five-inch running shorts with a liner and a zipped back pocket."),
        new("Training Shorts Olive", "apparel", "tidewell-athletics", 34.90m, 15, 0, new(Shorts, Fill: "#55603a"), 120,
            "Durable stretch training shorts for lifting and conditioning."),
        new("Lifting Tank Top", "apparel", "tidewell-athletics", 21.90m, 0, 4, new(Shirt, Fill: "#d1495b"), 105,
            "A breathable tank with a dropped armhole for full range of motion."),
        new("Sweat-Wicking Headband", "apparel", "tidewell-athletics", 9.90m, 0, 48, new(Band, "HEAD", "BAND", "#27272a"), 62,
            "A soft, wide headband that keeps sweat out of your eyes."),

        // Accessories
        new("Leakproof Shaker 700 ml", "accessories", "quillon-labs", 9.90m, 0, 120, new(Shaker), 260,
            "A leakproof shaker with a mixing ball and measurement markings."),
        new("Steel Shaker 750 ml", "accessories", "verdance-fuel", 19.90m, 0, 6, new(Shaker), 15,
            "Insulated stainless-steel shaker that keeps drinks cold for hours."),
        new("Lever Lifting Belt", "accessories", "brakken-barbell-co", 79.90m, 0, 12, new(Band, "LEVER", "BELT", "#27272a"), 88,
            "10 mm leather belt with a quick-release lever buckle."),
        new("Wrist Wraps Pair", "accessories", "brakken-barbell-co", 14.90m, 0, 40, new(Band, "WRIST", "WRAPS"), 52,
            "Stiff wrist wraps with thumb loops for heavy pressing."),
        new("Resistance Band Set", "accessories", "tidewell-athletics", 29.90m, 10, 26, new(Band, "5 LEVELS"), 52,
            "Five loop bands from light to extra heavy, with a carry pouch."),
        new("Extra-Thick Yoga Mat", "accessories", "tidewell-athletics", 34.90m, 0, 13, new(Mat), 98,
            "A 10 mm non-slip mat for mobility work and floor exercises."),
        new("Foam Roller 45 cm", "accessories", "orrin-cardio", 24.90m, 0, 1, new(Mat, Fill: "#27272a"), 7,
            "Firm foam roller with a textured surface for recovery."),
        new("Duffel Gym Bag 40 L", "accessories", "tidewell-athletics", 54.90m, 25, 10, new(Bag), 30,
            "Water-resistant duffel with a shoe compartment and padded strap."),
        new("Chalk Block 56 g", "accessories", "brakken-barbell-co", 3.90m, 0, 150, new(Jar, "CHALK", "56 G"), 140,
            "Magnesium carbonate block for a dry, secure grip."),
    ];
}
