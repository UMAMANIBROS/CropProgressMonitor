using CropProgressMonitor.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;

namespace CropProgressMonitor.Data;

/// <summary>
/// Seeds roles, default users, sample crops, phenological stages, and multilingual advisories.
/// </summary>
public static class DbInitializer
{
    public const string AdminRole = "Admin";
    public const string FarmerRole = "Farmer";

    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        using var scope = serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<Farmer>>();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("DbInitializer");

        try
        {
            if (context.Database.IsRelational())
            {
                await context.Database.MigrateAsync();
            }
            else
            {
                await context.Database.EnsureCreatedAsync();
            }
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "An error occurred while migrating the database.");
            throw;
        }

        // Seed Roles
        foreach (var role in new[] { AdminRole, FarmerRole })
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }

        // Seed Admin User
        const string adminEmail = "admin@cropprogress.local";
        var adminUser = await userManager.FindByEmailAsync(adminEmail);
        if (adminUser == null)
        {
            adminUser = new Farmer
            {
                UserName = adminEmail,
                Email = adminEmail,
                Name = "Agronomy Admin",
                Location = "Phagwara, Punjab",
                PreferredLanguage = "en",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(adminUser, "Admin@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(adminUser, AdminRole);
            }
        }

        // Seed Demo Farmer User
        const string demoEmail = "farmer@cropprogress.local";
        var demoFarmer = await userManager.FindByEmailAsync(demoEmail);
        if (demoFarmer == null)
        {
            demoFarmer = new Farmer
            {
                UserName = demoEmail,
                Email = demoEmail,
                Name = "Harpreet Singh",
                Location = "Phagwara, Punjab",
                PreferredLanguage = "en",
                EmailConfirmed = true
            };
            var result = await userManager.CreateAsync(demoFarmer, "Farmer@123");
            if (result.Succeeded)
            {
                await userManager.AddToRoleAsync(demoFarmer, FarmerRole);
            }
        }

        // NOTE: Base temperatures and GDD stage thresholds below are illustrative values for
        // academic demonstration and must be validated against local agronomic references
        // (such as ICAR / state agricultural university recommendations) before operational use.
        if (!await context.Crops.AnyAsync())
        {
            var crops = BuildSeedCrops();
            await context.Crops.AddRangeAsync(crops);
            await context.SaveChangesAsync();
        }

        // Seed sample fields for the demo farmer if they have none yet
        if (demoFarmer != null && !await context.Fields.AnyAsync(f => f.FarmerId == demoFarmer.Id))
        {
            var wheat = await context.Crops.FirstOrDefaultAsync(c => c.Name == "Wheat");
            var maize = await context.Crops.FirstOrDefaultAsync(c => c.Name == "Maize");

            if (wheat != null && maize != null)
            {
                var today = DateTime.UtcNow.Date;
                var northPlot = new Field
                {
                    FarmerId = demoFarmer.Id,
                    FieldName = "North Canal Plot",
                    LocationName = "Phagwara, Punjab, India",
                    Latitude = 31.2240,
                    Longitude = 75.7708
                };

                var riverPlot = new Field
                {
                    FarmerId = demoFarmer.Id,
                    FieldName = "East Loam Block",
                    LocationName = "Ludhiana, Punjab, India",
                    Latitude = 30.9010,
                    Longitude = 75.8573
                };

                context.Fields.AddRange(northPlot, riverPlot);
                await context.SaveChangesAsync();

                context.FieldCrops.AddRange(
                    new FieldCrop
                    {
                        FieldId = northPlot.Id,
                        CropId = wheat.Id,
                        Variety = "HD-3086",
                        SowingDate = today.AddDays(-35),
                        IsActive = true
                    },
                    new FieldCrop
                    {
                        FieldId = riverPlot.Id,
                        CropId = maize.Id,
                        Variety = "PMH-1",
                        SowingDate = today.AddDays(-22),
                        IsActive = true
                    }
                );
                await context.SaveChangesAsync();
            }
        }
    }

    private static List<Crop> BuildSeedCrops()
    {
        return new List<Crop>
        {
            CreateCrop(
                name: "Wheat",
                baseTemp: 5.0,
                description: "Rabi cereal crop sensitive to terminal heat during grain filling.",
                thresholds: new (string Stage, double Gdd, string Water, string Nutrient, string Risk,
                    string WaterHi, string NutrientHi, string RiskHi,
                    string WaterPa, string NutrientPa, string RiskPa)[]
                {
                    ("Sowing", 0,
                     "Ensure adequate pre-sowing soil moisture (palewa) for uniform seedbed preparation.",
                     "Apply basal dose of phosphorus, potash, and half nitrogen at sowing.",
                     "Use treated certified seed to prevent loose smut and termite damage.",
                     "बुवाई से पहले समान नमी (पलेवा) सुनिश्चित करें।",
                     "बुवाई के समय फॉस्फोरस, पोटाश और आधी नाइट्रोजन की आधार खुराक दें।",
                     "दीमक और कंडुआ रोग से बचाव के लिए उपचारित बीज बोएं।",
                     "ਬਿਜਾਈ ਤੋਂ ਪਹਿਲਾਂ ਖੇਤ ਵਿੱਚ ਇਕਸਾਰ ਨਮੀ (ਪਲੇਵਾ) ਯਕੀਨੀ ਬਣਾਓ।",
                     "ਬਿਜਾਈ ਸਮੇਂ ਫਾਸਫੋਰਸ, ਪੋਟਾਸ਼ ਅਤੇ ਅੱਧੀ ਨਾਈਟ੍ਰੋਜਨ ਦੀ ਬੁਨਿਆਦੀ ਖੁਰਾਕ ਪਾਓ।",
                     "ਦੀਮਕ ਅਤੇ ਕਾਂਗਿਆਰੀ ਤੋਂ ਬਚਾਅ ਲਈ ਸੋਧਿਆ ਹੋਇਆ ਬੀਜ ਵਰਤੋ।"),

                    ("Germination", 120,
                     "Avoid waterlogging; provide light irrigation only if topsoil crusts before emergence.",
                     "No top-dressing required yet; rely on basal starter nutrients.",
                     "Monitor for early damping-off, birds, and root aphids.",
                     "जलभराव से बचें; अंकुरण से पहले पपड़ी बनने पर हल्की सिंचाई करें।",
                     "अभी ऊपरी खाद की आवश्यकता नहीं है; आधार उर्वरक पर्याप्त है।",
                     "अंकुरण गलन, पक्षियों और जड़ माहू की निगरानी करें।",
                     "ਪਾਣੀ ਖੜ੍ਹਨ ਤੋਂ ਬਚੋ; ਉੱਗਣ ਤੋਂ ਪਹਿਲਾਂ ਕਰੰਡ ਬਣੇ ਤਾਂ ਹਲਕੀ ਸਿੰਚਾਈ ਕਰੋ।",
                     "ਇਸ ਸਮੇਂ ਉੱਪਰੋਂ ਖਾਦ ਦੀ ਲੋੜ ਨਹੀਂ; ਬੁਨਿਆਦੀ ਖੁਰਾਕ ਕਾਫ਼ੀ ਹੈ।",
                     "ਸ਼ੁਰੂਆਤੀ ਗਲਣ, ਪੰਛੀਆਂ ਅਤੇ ਜੜ੍ਹ ਦੇ ਤੇਲੇ ਦੀ ਨਿਗਰਾਨੀ ਕਰੋ।"),

                    ("Vegetative Growth", 380,
                     "Crown Root Initiation (CRI) and tillering need timely irrigation without moisture stress.",
                     "Top-dress remaining nitrogen after first irrigation; check zinc deficiency.",
                     "Scout for broadleaf weeds and early yellow rust patches.",
                     "शिखर जड़ बनने (CRI) और कल्ले निकलने पर समय पर सिंचाई करें।",
                     "पहली सिंचाई के बाद शेष नाइट्रोजन दें और जिंक की कमी जांचें।",
                     "चौड़ी पत्ती वाले खरपतवार और पीला रतुआ के शुरुआती लक्षण देखें।",
                     "ਮੁੱਢਲੀਆਂ ਜੜ੍ਹਾਂ (CRI) ਅਤੇ ਸ਼ਾਖਾਵਾਂ ਨਿਕਲਣ ਸਮੇਂ ਸਮੇਂ ਸਿਰ ਸਿੰਚਾਈ ਕਰੋ।",
                     "ਪਹਿਲੀ ਸਿੰਚਾਈ ਤੋਂ ਬਾਅਦ ਬਾਕੀ ਨਾਈਟ੍ਰੋਜਨ ਪਾਓ ਅਤੇ ਜ਼ਿੰਕ ਦੀ ਘਾਟ ਜਾਂਚੋ।",
                     "ਚੌੜੇ ਪੱਤੇ ਵਾਲੇ ਨਦੀਨਾਂ ਅਤੇ ਪੀਲੀ ਕੁੰਗੀ ਦੇ ਸ਼ੁਰੂਆਤੀ ਲੱਛਣਾਂ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Flowering", 820,
                     "Maintain steady soil moisture during booting and anthesis to avoid sterile florets.",
                     "Foliar potassium nitrate or micronutrient spray may support ear development if deficient.",
                     "Watch for aphids on earheads and avoid spraying pesticides during peak bee activity.",
                     "बाली निकलने और फूल आने के समय मिट्टी में स्थिर नमी बनाए रखें।",
                     "कमी दिखने पर पत्तियों पर पोटैशियम नाइट्रेट का छिड़काव लाभकारी हो सकता है।",
                     "बालियों पर माहू (चेपा) की जांच करें और हवा चलने पर छिड़काव टालें।",
                     "ਸਿੱਟੇ ਨਿਕਲਣ ਅਤੇ ਫੁੱਲ ਪੈਣ ਸਮੇਂ ਮਿੱਟੀ ਵਿੱਚ ਲਗਾਤਾਰ ਨਮੀ ਰੱਖੋ।",
                     "ਘਾਟ ਹੋਣ ਤੇ ਪੱਤਿਆਂ ਉੱਤੇ ਪੋਟਾਸ਼ੀਅਮ ਨਾਈਟ੍ਰੇਟ ਦਾ ਛਿੜਕਾਅ ਮਦਦਗਾਰ ਹੋ ਸਕਦਾ ਹੈ।",
                     "ਸਿੱਟਿਆਂ ਉੱਤੇ ਤੇਲੇ ਦੀ ਜਾਂਚ ਕਰੋ ਅਤੇ ਤੇਜ਼ ਹਵਾ ਜਾਂ ਮੀਂਹ ਵੇਲੇ ਛਿੜਕਾਅ ਨਾ ਕਰੋ।"),

                    ("Grain/Fruit Development", 1180,
                     "Ensure critical irrigation at milk and dough stages; avoid irrigation during high winds to prevent lodging.",
                     "Nutrient uptake is mostly complete; avoid late heavy nitrogen applications.",
                     "Monitor for Karnal bunt, aphids, and lodging under stormy conditions.",
                     "दूधिया और दाने भरने की अवस्था में सिंचाई करें, पर तेज हवा में सिंचाई से बचें।",
                     "पोषक तत्वों का अवशोषण लगभग पूरा हो चुका है; देर से अधिक नाइट्रोजन न दें।",
                     "करनाल बंट और फसल गिरने (लॉजिंग) के जोखिम से बचाव रखें।",
                     "ਦੁੱਧੀਆਂ ਅਤੇ ਦਾਣੇ ਭਰਨ ਦੀ ਅਵਸਥਾ ਵਿੱਚ ਸਿੰਚਾਈ ਕਰੋ, ਪਰ ਤੇਜ਼ ਹਵਾ ਵੇਲੇ ਪਾਣੀ ਨਾ ਲਾਓ।",
                     "ਖੁਰਾਕੀ ਤੱਤਾਂ ਦੀ ਲੋੜ ਲਗਭਗ ਪੂਰੀ ਹੈ; ਦੇਰ ਨਾਲ ਜ਼ਿਆਦਾ ਨਾਈਟ੍ਰੋਜਨ ਨਾ ਪਾਓ।",
                     "ਕਰਨਾਲ ਬੰਟ ਅਤੇ ਤੂਫ਼ਾਨੀ ਹਵਾਵਾਂ ਵਿੱਚ ਫ਼ਸਲ ਡਿੱਗਣ ਤੋਂ ਬਚਾਅ ਰੱਖੋ।"),

                    ("Maturity", 1500,
                     "Stop irrigation once grains harden and crop turns golden yellow.",
                     "Do not apply any fertilizers at physiological maturity.",
                     "Protect mature crop from hail, unseasonal rain, and shattering losses.",
                     "दाने सख्त होने और फसल सुनहरी होने पर सिंचाई बंद कर दें।",
                     "पकने की अवस्था में किसी भी प्रकार की खाद न डालें।",
                     "बेमौसम बारिश, ओलावृष्टि और दाने झड़ने से फसल की सुरक्षा करें।",
                     "ਦਾਣੇ ਸਖ਼ਤ ਹੋਣ ਅਤੇ ਫ਼ਸਲ ਸੁਨਹਿਰੀ ਹੋਣ ਤੇ ਸਿੰਚਾਈ ਬੰਦ ਕਰ ਦਿਓ।",
                     "ਪੱਕਣ ਦੀ ਅਵਸਥਾ ਵਿੱਚ ਕੋਈ ਵੀ ਖਾਦ ਨਾ ਪਾਓ।",
                     "ਬੇਮੌਸਮੀ ਮੀਂਹ, ਗੜ੍ਹੇਮਾਰੀ ਅਤੇ ਦਾਣੇ ਝੜਨ ਤੋਂ ਫ਼ਸਲ ਦੀ ਰੱਖਿਆ ਕਰੋ।"),

                    ("Harvest", 1720,
                     "Keep field dry to allow clean mechanical harvesting and safe grain moisture (<12-14%).",
                     "Incorporate crop residue or plan green manuring after harvest for soil health.",
                     "Store dried grain in clean, pest-free bags away from moisture.",
                     "कटाई के लिए खेत सूखा रखें और दाने की नमी 12-14% से कम रखें।",
                     "कटाई के बाद फसल अवशेष प्रबंधन से मिट्टी की उर्वरता बढ़ाएं।",
                     "सूखे अनाज को नमी और भंडारण कीटों से सुरक्षित स्थान पर रखें।",
                     "ਵਾਢੀ ਲਈ ਖੇਤ ਸੁੱਕਾ ਰੱਖੋ ਅਤੇ ਦਾਣੇ ਦੀ ਨਮੀ 12-14% ਤੋਂ ਘੱਟ ਰੱਖੋ।",
                     "ਵਾਢੀ ਤੋਂ ਬਾਅਦ ਪਰਾਲੀ ਨੂੰ ਖੇਤ ਵਿੱਚ ਮਿਲਾ ਕੇ ਮਿੱਟੀ ਦੀ ਸਿਹਤ ਸੁਧਾਰੋ।",
                     "ਸੁੱਕੇ ਦਾਣਿਆਂ ਨੂੰ ਨਮੀ ਅਤੇ ਗੋਦਾਮ ਦੇ ਕੀੜਿਆਂ ਤੋਂ ਬਚਾ ਕੇ ਸਟੋਰ ਕਰੋ।")
                }),

            CreateCrop(
                name: "Rice",
                baseTemp: 10.0,
                description: "Kharif staple crop with high sensitivity to water management and panicle temperature.",
                thresholds: new (string Stage, double Gdd, string Water, string Nutrient, string Risk,
                    string WaterHi, string NutrientHi, string RiskHi,
                    string WaterPa, string NutrientPa, string RiskPa)[]
                {
                    ("Sowing", 0,
                     "Prepare puddled nursery bed or moist direct-seeded bed with level drainage.",
                     "Apply farmyard manure and basal phosphorus/potash in the nursery or main field.",
                     "Treat seed against foot rot (bakanae) and blast before sowing.",
                     "समतल नर्सरी या सीधी बुवाई वाले खेत में उचित नमी तैयार करें।",
                     "नर्सरी में गोबर की खाद तथा फॉस्फोरस/पोटाश की आधार मात्रा डालें।",
                     "बकाने और झुलसा रोग से बचाव हेतु बीजोपचार अवश्य करें।",
                     "ਪੱਧਰੀ ਪਨੀਰੀ ਜਾਂ ਸਿੱਧੀ ਬਿਜਾਈ ਵਾਲੇ ਖੇਤ ਵਿੱਚ ਢੁਕਵੀਂ ਨਮੀ ਤਿਆਰ ਕਰੋ।",
                     "ਪਨੀਰੀ ਵਿੱਚ ਗੋਬਰ ਦੀ ਰੂੜੀ ਅਤੇ ਬੁਨਿਆਦੀ ਫਾਸਫੋਰਸ/ਪੋਟਾਸ਼ ਪਾਓ।",
                     "ਝੰਡਾ ਰੋਗ (ਬਕਾਨੇ) ਅਤੇ ਭੁਰੜ ਰੋਗ ਤੋਂ ਬਚਾਅ ਲਈ ਬੀਜ ਸੋਧ ਕੇ ਬੀਜੋ।"),

                    ("Germination", 140,
                     "Keep soil saturated without deep submergence so emerging seedlings breathe well.",
                     "Apply light nitrogen starter in nursery only if seedlings appear pale.",
                     "Guard against thrips, armyworm, and early weed competition.",
                     "मिट्टी को नम रखें लेकिन अधिक गहरा पानी न भरें ताकि अंकुर सांस ले सकें।",
                     "पौध पीली दिखने पर ही नर्सरी में हल्की नाइट्रोजन दें।",
                     "थ्रिप्स, सैनिक कीट और शुरुआती खरपतवारों पर नजर रखें।",
                     "ਮਿੱਟੀ ਨੂੰ ਗਿੱਲੀ ਰੱਖੋ ਪਰ ਜ਼ਿਆਦਾ ਡੂੰਘਾ ਪਾਣੀ ਨਾ ਖੜ੍ਹਾ ਕਰੋ।",
                     "ਜੇ ਪਨੀਰੀ ਪੀਲੀ ਲੱਗੇ ਤਾਂ ਹੀ ਹਲਕੀ ਨਾਈਟ੍ਰੋਜਨ ਪਾਓ।",
                     "ਥ੍ਰਿਪਸ, ਸੈਨਿਕ ਸੁੰਡੀ ਅਤੇ ਸ਼ੁਰੂਆਤੀ ਨਦੀਨਾਂ ਤੋਂ ਬਚਾਅ ਰੱਖੋ।"),

                    ("Vegetative Growth", 520,
                     "Maintain 2-5 cm standing water or alternate wetting and drying after establishment.",
                     "Split-apply nitrogen at active tillering; monitor for zinc deficiency (Khaira).",
                     "Scout for stem borer dead-hearts, leaf folder, and bacterial leaf blight.",
                     "रोपाई के बाद 2-5 सेमी पानी या वैकल्पिक गीला-सूखा चक्र अपनाएं।",
                     "कल्ले निकलते समय नाइट्रोजन की दूसरी किस्त दें और जिंक की जांच करें।",
                     "तना छेदक, पत्ती लपेटक और जीवाणु झुलसा रोग की निगरानी करें।",
                     "ਲੁਆਈ ਤੋਂ ਬਾਅਦ 2-5 ਸੈਂਟੀਮੀਟਰ ਪਾਣੀ ਜਾਂ ਵਾਰੀ-ਸਿਰ ਸੁਕਾਉਣ ਦਾ ਤਰੀਕਾ ਅਪਣਾਓ।",
                     "ਸ਼ਾਖਾਵਾਂ ਨਿਕਲਣ ਸਮੇਂ ਨਾਈਟ੍ਰੋਜਨ ਦੀ ਕਿਸ਼ਤ ਪਾਓ ਅਤੇ ਜ਼ਿੰਕ ਦੀ ਘਾਟ ਜਾਂਚੋ।",
                     "ਗੋਭ ਦੀ ਸੁੰਡੀ, ਪੱਤਾ ਲਪੇਟ ਸੁੰਡੀ ਅਤੇ ਝੁਲਸ ਰੋਗ ਦੀ ਨਿਗਰਾਨੀ ਕਰੋ।"),

                    ("Flowering", 1050,
                     "Avoid drought stress completely during panicle initiation and flowering.",
                     "Apply final panicle-initiation nitrogen/potash only as recommended for variety.",
                     "Inspect for brown planthopper, false smut, and neck blast.",
                     "बाली बनते और फूल आते समय खेत में नमी की कमी बिल्कुल न होने दें।",
                     "किस्म की सिफारिश के अनुसार अंतिम नाइट्रोजन/पोटाश का प्रयोग करें।",
                     "भूरा माहू, आभासी कंडुआ और गर्दन तोड़ झुलसा के लिए जांच करें।",
                     "ਗੋਭ ਅਤੇ ਫੁੱਲ ਆਉਣ ਸਮੇਂ ਖੇਤ ਵਿੱਚ ਪਾਣੀ ਦੀ ਘਾਟ ਬਿਲਕੁਲ ਨਾ ਹੋਣ ਦਿਓ।",
                     "ਕਿਸਮ ਦੀ ਸਿਫ਼ਾਰਸ਼ ਅਨੁਸਾਰ ਆਖ਼ਰੀ ਨਾਈਟ੍ਰੋਜਨ/ਪੋਟਾਸ਼ ਵਰਤੋ।",
                     "ਭੂਰੇ ਤੇਲੇ, ਹਲਦੀ ਰੋਗ ਅਤੇ ਗਰਦਨ ਤੋੜ ਭੁਰੜ ਰੋਗ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Grain/Fruit Development", 1450,
                     "Maintain shallow moisture through milky stage; begin gradual drainage at hard dough.",
                     "Foliar nutrition is rarely needed; avoid late nitrogen that attracts pests.",
                     "Watch for grain discoloration, earhead bug, and lodging after heavy showers.",
                     "दूधिया अवस्था तक हल्की नमी रखें और दाने भरने के बाद धीरे-धीरे पानी निकालें।",
                     "देर से नाइट्रोजन न डालें, इससे कीटों का प्रकोप बढ़ सकता है।",
                     "दाने के बदरंग होने, गंधी बग और भारी बारिश में फसल गिरने से बचाएं।",
                     "ਦੁੱਧੀਆਂ ਅਵਸਥਾ ਤੱਕ ਹਲਕੀ ਨਮੀ ਰੱਖੋ ਅਤੇ ਦਾਣੇ ਸਖ਼ਤ ਹੋਣ ਤੇ ਪਾਣੀ ਕੱਢ ਦਿਓ।",
                     "ਦੇਰ ਨਾਲ ਨਾਈਟ੍ਰੋਜਨ ਨਾ ਪਾਓ ਕਿਉਂਕਿ ਇਸ ਨਾਲ ਕੀੜੇ ਵੱਧਦੇ ਹਨ।",
                     "ਦਾਣੇ ਖ਼ਰਾਬ ਹੋਣ, ਗੰਧੀ ਬੱਗ ਅਤੇ ਭਾਰੀ ਮੀਂਹ ਵਿੱਚ ਫ਼ਸਲ ਡਿੱਗਣ ਤੋਂ ਬਚਾਓ।"),

                    ("Maturity", 1780,
                     "Drain the field 10-15 days before expected harvest for uniform ripening.",
                     "No fertilizer inputs needed during ripening.",
                     "Monitor weather forecasts to avoid harvesting right before heavy rain.",
                     "समान पकने के लिए कटाई से 10-15 दिन पहले खेत से पानी निकाल दें।",
                     "पकने के चरण में किसी खाद की आवश्यकता नहीं है।",
                     "कटाई से पहले भारी वर्षा के पूर्वानुमान पर ध्यान दें।",
                     "ਇਕਸਾਰ ਪੱਕਣ ਲਈ ਵਾਢੀ ਤੋਂ 10-15 ਦਿਨ ਪਹਿਲਾਂ ਖੇਤ ਵਿੱਚੋਂ ਪਾਣੀ ਕੱਢ ਦਿਓ।",
                     "ਪੱਕਣ ਵੇਲੇ ਕਿਸੇ ਵੀ ਖਾਦ ਦੀ ਲੋੜ ਨਹੀਂ ਹੈ।",
                     "ਵਾਢੀ ਤੋਂ ਪਹਿਲਾਂ ਭਾਰੀ ਮੀਂਹ ਦੀ ਭਵਿੱਖਬਾਣੀ ਉੱਤੇ ਧਿਆਨ ਰੱਖੋ।"),

                    ("Harvest", 2050,
                     "Harvest when 80-85% spikelets turn straw-colored and dry paddy promptly.",
                     "Manage paddy straw via mulching or incorporation instead of burning.",
                     "Dry grains to ~14% moisture before bagging to prevent fungal spoilage.",
                     "80-85% बालियां सुनहरी होने पर कटाई करें और धान को शीघ्र सुखाएं।",
                     "पराली जलाने के बजाय मल्चिंग या खेत में मिलाने का उपयोग करें।",
                     "फफूंद से बचाने के लिए भंडारण से पहले नमी लगभग 14% तक सुखाएं।",
                     "80-85% ਮੁੰਜਰਾਂ ਸੁਨਹਿਰੀ ਹੋਣ ਤੇ ਵਾਢੀ ਕਰੋ ਅਤੇ ਝੋਨੇ ਨੂੰ ਜਲਦੀ ਸੁਕਾਓ।",
                     "ਪਰਾਲੀ ਨੂੰ ਸਾੜਨ ਦੀ ਬਜਾਏ ਮਲਚਿੰਗ ਜਾਂ ਖੇਤ ਵਿੱਚ ਮਿਲਾਉਣ ਦਾ ਤਰੀਕਾ ਵਰਤੋ।",
                     "ਉੱਲੀ ਤੋਂ ਬਚਾਅ ਲਈ ਸਟੋਰ ਕਰਨ ਤੋਂ ਪਹਿਲਾਂ ਨਮੀ 14% ਤੱਕ ਸੁਕਾਓ।")
                }),

            CreateCrop(
                name: "Maize",
                baseTemp: 10.0,
                description: "Warm-season cereal requiring well-drained soil and moisture at tasseling/silking.",
                thresholds: new (string Stage, double Gdd, string Water, string Nutrient, string Risk,
                    string WaterHi, string NutrientHi, string RiskHi,
                    string WaterPa, string NutrientPa, string RiskPa)[]
                {
                    ("Sowing", 0,
                     "Sow on ridges or well-drained beds so excess rainwater does not stagnate.",
                     "Apply basal phosphorus, potash, zinc sulfate, and 1/3 nitrogen at sowing.",
                     "Use fungicide-treated seed to guard against seed rot and seedling blight.",
                     "जलभराव से बचने के लिए मेड़ों या अच्छी जल निकासी वाली क्यारियों में बुवाई करें।",
                     "बुवाई पर फॉस्फोरस, पोटाश, जिंक सल्फेट और एक-तिहाई नाइट्रोजन दें।",
                     "बीज सड़न और अंकुर झुलसा से बचने के लिए उपचारित बीज प्रयोग करें।",
                     "ਪਾਣੀ ਖੜ੍ਹਨ ਤੋਂ ਬਚਾਅ ਲਈ ਵੱਟਾਂ ਜਾਂ ਚੰਗੇ ਨਿਕਾਸ ਵਾਲੇ ਖੇਤ ਵਿੱਚ ਬਿਜਾਈ ਕਰੋ।",
                     "ਬਿਜਾਈ ਸਮੇਂ ਫਾਸਫੋਰਸ, ਪੋਟਾਸ਼, ਜ਼ਿੰਕ ਸਲਫੇਟ ਅਤੇ 1/3 ਨਾਈਟ੍ਰੋਜਨ ਪਾਓ।",
                     "ਬੀਜ ਗਲਣ ਤੋਂ ਬਚਾਅ ਲਈ ਉੱਲੀਨਾਸ਼ਕ ਨਾਲ ਸੋਧਿਆ ਬੀਜ ਵਰਤੋ।"),

                    ("Germination", 110,
                     "Maintain gentle topsoil moisture; drain standing water immediately after heavy rain.",
                     "Hold top-dressing until plants reach 4-6 leaf stage.",
                     "Check for cutworms, shoot fly, and early weed emergence.",
                     "ऊपरी मिट्टी में हल्की नमी रखें; भारी बारिश के बाद पानी तुरंत निकालें।",
                     "4-6 पत्ती की अवस्था आने तक ऊपरी खाद रोक कर रखें।",
                     "कटुआ इल्ली, तना मक्खी और शुरुआती खरपतवारों की जांच करें।",
                     "ਉੱਪਰਲੀ ਮਿੱਟੀ ਵਿੱਚ ਹਲਕੀ ਨਮੀ ਰੱਖੋ; ਭਾਰੀ ਮੀਂਹ ਦਾ ਪਾਣੀ ਤੁਰੰਤ ਕੱਢੋ।",
                     "4-6 ਪੱਤੇ ਆਉਣ ਤੱਕ ਉੱਪਰੋਂ ਖਾਦ ਨਾ ਪਾਓ।",
                     "ਕੱਟੂਆ ਸੁੰਡੀ, ਸ਼ੂਟ ਫਲਾਈ ਅਤੇ ਸ਼ੁਰੂਆਤੀ ਨਦੀਨਾਂ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Vegetative Growth", 430,
                     "Irrigate at knee-high stage if dry spells persist; keep furrows clear for drainage.",
                     "Top-dress second split of nitrogen at knee-high stage (~30-35 days).",
                     "Scout whorls actively for Fall Armyworm damage and leaf blight.",
                     "सूखे की स्थिति में घुटने तक ऊंचाई पर सिंचाई करें और नालियां साफ रखें।",
                     "घुटने तक ऊंचाई (30-35 दिन) पर नाइट्रोजन की दूसरी खुराक दें।",
                     "पौधे की गोभ में फॉल आर्मीवर्म और पत्ती झुलसा की नियमित जांच करें।",
                     "ਸੋਕੇ ਦੀ ਸਥਿਤੀ ਵਿੱਚ ਗੋਡੇ-ਗੋਡੇ ਉਚਾਈ ਤੇ ਸਿੰਚਾਈ ਕਰੋ ਅਤੇ ਖਾਲੀਆਂ ਸਾਫ਼ ਰੱਖੋ।",
                     "ਗੋਡੇ ਜਿੰਨੀ ਉਚਾਈ (30-35 ਦਿਨ) ਤੇ ਨਾਈਟ੍ਰੋਜਨ ਦੀ ਦੂਜੀ ਕਿਸ਼ਤ ਪਾਓ।",
                     "ਗੋਭ ਵਿੱਚ ਫਾਲ ਆਰਮੀਵਰਮ ਸੁੰਡੀ ਅਤੇ ਪੱਤਾ ਝੁਲਸ ਰੋਗ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Flowering", 780,
                     "Tasseling and silking are peak water-demand stages; avoid moisture deficit.",
                     "Apply final nitrogen split just before tasseling if crop vigor is low.",
                     "Protect silks from beetles and monitor stem borer or banded leaf sheath blight.",
                     "नर मंजरी (टैसल) और रेशे निकलते समय पानी की कमी बिल्कुल न होने दें।",
                     "मंजरी निकलने से ठीक पहले आवश्यकतानुसार अंतिम नाइट्रोजन दें।",
                     "रेशों को नुकसान पहुंचाने वाले कीटों और तना छेदक पर नजर रखें।",
                     "ਬਾਬੂ ਝੰਡੇ ਅਤੇ ਵਾਲ ਨਿਕਲਣ ਸਮੇਂ ਪਾਣੀ ਦੀ ਕਮੀ ਬਿਲਕੁਲ ਨਾ ਹੋਣ ਦਿਓ।",
                     "ਝੰਡੇ ਨਿਕਲਣ ਤੋਂ ਪਹਿਲਾਂ ਲੋੜ ਅਨੁਸਾਰ ਆਖ਼ਰੀ ਨਾਈਟ੍ਰੋਜਨ ਪਾਓ।",
                     "ਵਾਲ ਖਾਣ ਵਾਲੇ ਕੀੜਿਆਂ ਅਤੇ ਤਣਾ ਛੇਦਕ ਦੀ ਨਿਗਰਾਨੀ ਕਰੋ।"),

                    ("Grain/Fruit Development", 1120,
                     "Maintain moisture during blister and milk stages for full cob tip filling.",
                     "Avoid late nitrogen; ensure potassium adequacy for stalk strength.",
                     "Monitor cob rot, birds, and lodging risk during thunderstorms.",
                     "दाने भरने और दूधिया अवस्था में भुट्टे के पूरे भराव हेतु नमी बनाए रखें।",
                     "देर से नाइट्रोजन न दें; तने की मजबूती के लिए पोटाश संतुलित रखें।",
                     "भुट्टा सड़न, पक्षियों और आंधी में पौधे गिरने से बचाव रखें।",
                     "ਦਾਣੇ ਭਰਨ ਅਤੇ ਦੁੱਧੀਆਂ ਅਵਸਥਾ ਵਿੱਚ ਛੱਲੀ ਦੇ ਪੂਰੇ ਭਰਾਅ ਲਈ ਨਮੀ ਰੱਖੋ।",
                     "ਦੇਰ ਨਾਲ ਨਾਈਟ੍ਰੋਜਨ ਨਾ ਪਾਓ; ਤਣੇ ਦੀ ਮਜ਼ਬੂਤੀ ਲਈ ਪੋਟਾਸ਼ ਸੰਤੁਲਿਤ ਰੱਖੋ।",
                     "ਛੱਲੀ ਗਲਣ, ਪੰਛੀਆਂ ਅਤੇ ਹਨੇਰੀ ਵਿੱਚ ਫ਼ਸਲ ਡਿੱਗਣ ਤੋਂ ਬਚਾਓ।"),

                    ("Maturity", 1420,
                     "Withdraw irrigation once husks dry and black layer forms at kernel base.",
                     "No fertilization required.",
                     "Prevent ear rot by avoiding field waterlogging near physiological maturity.",
                     "छिलका सूखने और दाने के आधार पर काली परत बनने पर सिंचाई बंद करें।",
                     "इस अवस्था में किसी उर्वरक की आवश्यकता नहीं है।",
                     "पकने के करीब खेत में जलभराव न होने दें ताकि भुट्टा सड़न से बचे।",
                     "ਛਿੱਲੜ ਸੁੱਕਣ ਅਤੇ ਦਾਣੇ ਹੇਠ ਕਾਲੀ ਪਰਤ ਬਣਨ ਤੇ ਸਿੰਚਾਈ ਬੰਦ ਕਰ ਦਿਓ।",
                     "ਇਸ ਸਮੇਂ ਕਿਸੇ ਖਾਦ ਦੀ ਲੋੜ ਨਹੀਂ ਹੈ।",
                     "ਪੱਕਣ ਦੇ ਨੇੜੇ ਖੇਤ ਵਿੱਚ ਪਾਣੀ ਨਾ ਖੜ੍ਹਨ ਦਿਓ ਤਾਂ ਜੋ ਛੱਲੀਆਂ ਨਾ ਗਲਣ।"),

                    ("Harvest", 1620,
                     "Harvest cobs when grain moisture drops below 20% and dry rapidly in sun/dryer.",
                     "Return stover as mulch or compost to replenish organic carbon.",
                     "Dry shelled kernels to 12% moisture to avoid aflatoxin during storage.",
                     "दाने की नमी 20% से नीचे आने पर भुट्टे तोड़ें और तुरंत सुखाएं।",
                     "डंठलों को मल्च या कम्पोस्ट के रूप में उपयोग कर मिट्टी सुधारें।",
                     "एफ्लाटॉक्सिन से बचाने हेतु दाने को 12% नमी तक सुखाकर भंडारित करें।",
                     "ਦਾਣੇ ਦੀ ਨਮੀ 20% ਤੋਂ ਘੱਟ ਹੋਣ ਤੇ ਛੱਲੀਆਂ ਤੋੜੋ ਅਤੇ ਜਲਦੀ ਸੁਕਾਓ।",
                     "ਟਾਂਡਿਆਂ ਨੂੰ ਮਲਚ ਜਾਂ ਖਾਦ ਵਜੋਂ ਵਰਤ ਕੇ ਮਿੱਟੀ ਦੀ ਉਪਜਾਊ ਸ਼ਕਤੀ ਵਧਾਓ।",
                     "ਐਫਲਾਟੌਕਸਿਨ ਤੋਂ ਬਚਾਅ ਲਈ ਦਾਣਿਆਂ ਨੂੰ 12% ਨਮੀ ਤੱਕ ਸੁਕਾ ਕੇ ਸਟੋਰ ਕਰੋ।")
                }),

            CreateCrop(
                name: "Cotton",
                baseTemp: 12.0,
                description: "Long-duration fiber crop sensitive to excess rain at boll opening and sucking pests.",
                thresholds: new (string Stage, double Gdd, string Water, string Nutrient, string Risk,
                    string WaterHi, string NutrientHi, string RiskHi,
                    string WaterPa, string NutrientPa, string RiskPa)[]
                {
                    ("Sowing", 0,
                     "Ensure good pre-sowing moisture for deep taproot establishment; avoid crusting.",
                     "Apply recommended phosphorus and potash as basal dose.",
                     "Select recommended Bt/non-Bt hybrid and sow refuge border as advised.",
                     "गहरी जड़ के लिए बुवाई से पहले अच्छी नमी रखें और पपड़ी बनने से बचाएं।",
                     "बुवाई के समय अनुशंसित फॉस्फोरस और पोटाश की आधार मात्रा दें।",
                     "अनुशंसित किस्म चुनें और कीट प्रतिरोध प्रबंधन हेतु रिफ्यूजिया पंक्तियां बोएं।",
                     "ਡੂੰਘੀ ਜੜ੍ਹ ਲਈ ਬਿਜਾਈ ਤੋਂ ਪਹਿਲਾਂ ਚੰਗੀ ਨਮੀ ਰੱਖੋ ਅਤੇ ਕਰੰਡ ਨਾ ਬਣਨ ਦਿਓ।",
                     "ਬਿਜਾਈ ਸਮੇਂ ਸਿਫ਼ਾਰਸ਼ ਕੀਤੀ ਫਾਸਫੋਰਸ ਅਤੇ ਪੋਟਾਸ਼ ਦੀ ਬੁਨਿਆਦੀ ਖੁਰਾਕ ਪਾਓ।",
                     "ਸਿਫ਼ਾਰਸ਼ ਕੀਤੀ ਕਿਸਮ ਚੁਣੋ ਅਤੇ ਬੀ.ਟੀ. ਪ੍ਰਬੰਧਨ ਲਈ ਰਿਫਿਊਜੀਆ ਕਤਾਰਾਂ ਬੀਜੋ।"),

                    ("Germination", 130,
                     "Do not flood young seedlings; provide drainage channels if heavy rain occurs.",
                     "Delay nitrogen top-dressing until thinning and weeding are done.",
                     "Inspect seedlings for root rot, thrips, and aphids.",
                     "छोटे पौधों में पानी न भरने दें; भारी वर्षा होने पर निकासी की व्यवस्था रखें।",
                     "छंटाई और निराई के बाद ही नाइट्रोजन की ऊपरी खुराक दें।",
                     "अंकुरों में जड़ गलन, थ्रिप्स और माहू की जांच करें।",
                     "ਨੰਨ੍ਹੇ ਪੌਦਿਆਂ ਵਿੱਚ ਪਾਣੀ ਨਾ ਖੜ੍ਹਨ ਦਿਓ; ਭਾਰੀ ਮੀਂਹ ਵੇਲੇ ਨਿਕਾਸ ਕਰੋ।",
                     "ਵਿਰਲਾ ਕਰਨ ਅਤੇ ਗੋਡੀ ਤੋਂ ਬਾਅਦ ਹੀ ਨਾਈਟ੍ਰੋਜਨ ਪਾਓ।",
                     "ਪੌਦਿਆਂ ਵਿੱਚ ਜੜ੍ਹ ਗਲਣ, ਥ੍ਰਿਪਸ ਅਤੇ ਤੇਲੇ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Vegetative Growth", 550,
                     "First irrigation typically 4-6 weeks after sowing depending on rain; avoid excess water.",
                     "Apply first nitrogen top-dressing after weeding/thinning.",
                     "Monitor whitefly, jassids, and leaf curl virus symptoms weekly.",
                     "मौसम अनुसार बुवाई के 4-6 सप्ताह बाद पहली सिंचाई करें; अधिक पानी से बचें।",
                     "निराई-गुड़ाई के बाद नाइट्रोजन की पहली ऊपरी खुराक दें।",
                     "सफेद मक्खी, हरा तेला और पत्ती मरोड़ वायरस की साप्ताहिक निगरानी करें।",
                     "ਮੌਸਮ ਅਨੁਸਾਰ ਬਿਜਾਈ ਤੋਂ 4-6 ਹਫ਼ਤੇ ਬਾਅਦ ਪਹਿਲੀ ਸਿੰਚਾਈ ਕਰੋ।",
                     "ਗੋਡੀ ਕਰਨ ਤੋਂ ਬਾਅਦ ਨਾਈਟ੍ਰੋਜਨ ਦੀ ਪਹਿਲੀ ਕਿਸ਼ਤ ਪਾਓ।",
                     "ਚਿੱਟੀ ਮੱਖੀ, ਹਰਾ ਤੇਲਾ ਅਤੇ ਪੱਤਾ ਮਰੋੜ ਵਾਇਰਸ ਦੀ ਹਫ਼ਤਾਵਾਰੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Flowering", 980,
                     "Maintain balanced moisture during squaring and flowering to prevent square drop.",
                     "Apply second nitrogen split and foliar magnesium/potassium if leaf reddening appears.",
                     "Install pheromone traps for pink bollworm and monitor whitefly thresholds.",
                     "कलियां और फूल बनते समय नमी संतुलित रखें ताकि कलियां न झड़ें।",
                     "पत्ते लाल दिखने पर नाइट्रोजन की दूसरी किस्त व मैग्नीशियम/पोटाश स्प्रे करें।",
                     "गुलाबी सुंडी के लिए फेरोमोन ट्रैप लगाएं और सफेद मक्खी का स्तर जांचें।",
                     "ਡੋਡੀਆਂ ਅਤੇ ਫੁੱਲ ਪੈਣ ਸਮੇਂ ਨਮੀ ਸੰਤੁਲਿਤ ਰੱਖੋ ਤਾਂ ਜੋ ਫੁੱਲ ਨਾ ਝੜਨ।",
                     "ਪੱਤੇ ਲਾਲ ਹੋਣ ਤੇ ਨਾਈਟ੍ਰੋਜਨ ਦੀ ਕਿਸ਼ਤ ਅਤੇ ਮੈਗਨੀਸ਼ੀਅਮ/ਪੋਟਾਸ਼ ਛਿੜਕੋ।",
                     "ਗੁਲਾਬੀ ਸੁੰਡੀ ਲਈ ਫੈਰੋਮੋਨ ਟਰੈਪ ਲਗਾਓ ਅਤੇ ਚਿੱਟੀ ਮੱਖੀ ਦੀ ਗਿਣਤੀ ਕਰੋ।"),

                    ("Grain/Fruit Development", 1450,
                     "Irrigate moderately during boll formation; avoid waterlogging which triggers boll rot.",
                     "Foliar potassium nitrate helps improve boll weight and fiber quality.",
                     "Scout green bolls for pink bollworm and fungal boll rot after rains.",
                     "टिंडे बनते समय संतुलित सिंचाई करें; जलभराव से टिंडा सड़न बढ़ सकती है।",
                     "पोटैशियम नाइट्रेट का पर्णीय छिड़काव टिंडे के वजन और रेशे की गुणवत्ता बढ़ाता है।",
                     "बारिश के बाद हरे टिंडों में गुलाबी सुंडी और सड़न की जांच करें।",
                     "ਟੀਂਡੇ ਬਣਨ ਸਮੇਂ ਸੰਤੁਲਿਤ ਸਿੰਚਾਈ ਕਰੋ; ਪਾਣੀ ਖੜ੍ਹਨ ਨਾਲ ਟੀਂਡੇ ਗਲ ਸਕਦੇ ਹਨ।",
                     "ਪੋਟਾਸ਼ੀਅਮ ਨਾਈਟ੍ਰੇਟ ਦਾ ਛਿੜਕਾਅ ਟੀਂਡੇ ਦੇ ਭਾਰ ਅਤੇ ਰੇਸ਼ੇ ਦੀ ਗੁਣਵੱਤਾ ਵਧਾਉਂਦਾ ਹੈ।",
                     "ਮੀਂਹ ਤੋਂ ਬਾਅਦ ਹਰੇ ਟੀਂਡਿਆਂ ਵਿੱਚ ਗੁਲਾਬੀ ਸੁੰਡੀ ਅਤੇ ਗਲਣ ਦੀ ਜਾਂਚ ਕਰੋ।"),

                    ("Maturity", 1900,
                     "Stop irrigation once ~30-40% bolls begin opening to support clean picking.",
                     "No fertilizer application at boll opening.",
                     "Keep field dry and watch forecasts to pick open bolls before rain stains lint.",
                     "30-40% टिंडे खिलने पर सिंचाई बंद कर दें ताकि साफ चुनाई हो सके।",
                     "टिंडे खिलने की अवस्था में खाद न दें।",
                     "बारिश से रेशा खराब होने से पहले खुले टिंडों की समय पर चुनाई करें।",
                     "30-40% ਟੀਂਡੇ ਖਿੜਨ ਤੇ ਸਿੰਚਾਈ ਬੰਦ ਕਰ ਦਿਓ ਤਾਂ ਜੋ ਸਾਫ਼ ਚੁਗਾਈ ਹੋ ਸਕੇ।",
                     "ਟੀਂਡੇ ਖਿੜਨ ਵੇਲੇ ਕੋਈ ਖਾਦ ਨਾ ਪਾਓ।",
                     "ਮੀਂਹ ਨਾਲ ਰੂੰ ਖ਼ਰਾਬ ਹੋਣ ਤੋਂ ਪਹਿਲਾਂ ਖਿੜੇ ਟੀਂਡਿਆਂ ਦੀ ਚੁਗਾਈ ਕਰੋ।"),

                    ("Harvest", 2250,
                     "Pick dry cotton after morning dew evaporates; keep clean and stained lint separate.",
                     "Shred and incorporate cotton stalks or manage pink bollworm carryover off-season.",
                     "Store dry seed cotton in ventilated dry rooms away from moisture.",
                     "सुबह की ओस सूखने के बाद सूखी कपास चुनें और साफ व दागदार कपास अलग रखें।",
                     "चुनाई के बाद डंठलों का उचित प्रबंधन करें ताकि गुलाबी सुंडी अगले मौसम में न फैले।",
                     "सूखी कपास को हवादार और सूखे स्थान पर सुरक्षित संग्रहित करें।",
                     "ਸਵੇਰ ਦੀ ਤ੍ਰੇਲ ਸੁੱਕਣ ਤੋਂ ਬਾਅਦ ਸੁੱਕਾ ਨਰਮਾ ਚੁਗੋ ਅਤੇ ਸਾਫ਼ ਤੇ ਦਾਗ਼ੀ ਨਰਮਾ ਵੱਖ ਰੱਖੋ।",
                     "ਚੁਗਾਈ ਤੋਂ ਬਾਅਦ ਛਿਟੀਆਂ ਦਾ ਸਹੀ ਪ੍ਰਬੰਧਨ ਕਰੋ ਤਾਂ ਜੋ ਗੁਲਾਬੀ ਸੁੰਡੀ ਅੱਗੇ ਨਾ ਫੈਲੇ।",
                     "ਸੁੱਕੇ ਨਰਮੇ ਨੂੰ ਹਵਾਦਾਰ ਅਤੇ ਸੁੱਕੇ ਕਮਰੇ ਵਿੱਚ ਸਟੋਰ ਕਰੋ।")
                })
        };
    }

    private static Crop CreateCrop(
        string name,
        double baseTemp,
        string description,
        (string Stage, double Gdd, string Water, string Nutrient, string Risk,
            string WaterHi, string NutrientHi, string RiskHi,
            string WaterPa, string NutrientPa, string RiskPa)[] thresholds)
    {
        var crop = new Crop
        {
            Name = name,
            BaseTemperature = baseTemp,
            Description = description
        };

        for (int i = 0; i < thresholds.Length; i++)
        {
            var item = thresholds[i];
            var stage = new CropStage
            {
                StageOrder = i + 1,
                StageName = item.Stage,
                GddThreshold = item.Gdd
            };

            stage.Advisories.Add(new Advisory
            {
                WaterAdvice = item.Water,
                NutrientAdvice = item.Nutrient,
                RiskAdvice = item.Risk,
                WaterAdviceHi = item.WaterHi,
                NutrientAdviceHi = item.NutrientHi,
                RiskAdviceHi = item.RiskHi,
                WaterAdvicePa = item.WaterPa,
                NutrientAdvicePa = item.NutrientPa,
                RiskAdvicePa = item.RiskPa
            });

            crop.CropStages.Add(stage);
        }

        return crop;
    }
}
