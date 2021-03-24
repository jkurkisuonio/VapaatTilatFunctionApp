using Azure.Storage.Files.Shares;
using Azure.Storage.Files.Shares.Models;
using EdupoliBizLib.Careeria;
using EdupoliBizLib.Models.Careeria;
using EdupoliBizLib.Models.Wilma;
using EdupoliBizLib.Primus;
using EdupoliBizLib.Util;
using EdupoliBizLib.Wilma;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;


namespace VapaatTilatFunctionApp
{
    public class WilmaResurssiLaskenta : IWilmaResurssiLaskenta
    {
        private readonly WilmaJson wilma;
        private readonly string careeriaRoomJsonFileLocation;
        public WilmaResurssiLaskenta(IConfigurationRoot config)
        {
            // Otetaan yhteys Wilmaan.
            // Luodaan WilmaJson luokasta olio ja alustetaan se wilmapalvelimen Urlilla, salasanalla, käyttäjätunnuksella ja Wilma-avaimella.

  

            wilma = new WilmaJson(config["wilmaUrl"], 
                                  config["wilmaPasswd"],
                                  config["wilmaUsername"],
                                  config["wilmaCompanySpesificKey"]);
            // Luodaan sessio.
            try
            {
                string firstContact = wilma.Login(string.Empty);
                // Kirjaudutaan
                string loginWCookiesResult = wilma.LoginWCookies(config["wilmaUrl"] + "login");
            }
            catch (Exception ex)
            {
                Debug.WriteLine(" Exception. EX message: " + ex.Message.ToString());
                string firstContact = wilma.Login(string.Empty);
                // Kirjaudutaan
                string loginWCookiesResult = wilma.LoginWCookies(config["wilmaUrl"] + "login");
            }           
            
            careeriaRoomJsonFileLocation = config["careeriaRoomJsonFileLocation"];
            // Ladataan CareeriaRooms.Json Azure File Storagesta.
            // https://docs.microsoft.com/en-us/dotnet/api/overview/azure/storage.files.shares-readme
            //
            string connectionString = config["fileStorageConnectionString"];

            // Name of the share, directory, and file we'll download from
            string shareName = "wilmafileshare";
            string dirName = "json";
            string fileName = "careeriarooms.json";

            // Path to the save the downloaded file
            string localFilePath = careeriaRoomJsonFileLocation;

            // Get a reference to the file
            ShareClient share = new ShareClient(connectionString, shareName);
            ShareDirectoryClient directory = share.GetDirectoryClient(dirName);
            ShareFileClient file = directory.GetFileClient(fileName);

            // Download the file
            ShareFileDownloadInfo download = file.Download();
            using (FileStream stream = File.OpenWrite(localFilePath))
            {
                download.Content.CopyTo(stream);
            }
        }

        public ResurssiTilatModel CountRuokailijat(string alkupvm, string paattyenpvm)
        {
            ResurssiTilatModel model = new ResurssiTilatModel();
            //Selvitetään mistä viikosta on kyse. Tämä pitäisi myös Javascriptillä saada näkyviin.
            DateTime alkuDate, viikonEkaPvm = DateTime.Now, viikonVikaPvm = DateTime.Now;
            bool alku = DateTime.TryParse(alkupvm, out alkuDate);
            if (alku)
            {
                int viikkoNro = EdupoliBizLib.Util.DateOp.GetIso8601WeekOfYear(alkuDate);
                viikonEkaPvm = EdupoliBizLib.Util.DateOp.FirstDateOfWeekISO8601(alkuDate.Year, viikkoNro);
                viikonVikaPvm = viikonEkaPvm.AddDays(5);
                model.ViikkoAlkaaPvm = viikonEkaPvm;
                model.ViikkoLoppuuPvm = viikonVikaPvm;
                model.ViikkoNro = viikkoNro;

            }

            model.RuokalijaPaivaSaldo[0] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[1] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[2] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[3] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[4] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[5] = new Ruokailijat();

            model.RuokalijaPaivaSaldo[6] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[7] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[8] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[9] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[10] = new Ruokailijat();
            model.RuokalijaPaivaSaldo[11] = new Ruokailijat();

            // Haetaan kaikki tilat annettujen parametrien perusteella
            var result2 = wilma.Login("schedule/index_json?p=" + viikonEkaPvm.ToShortDateString() + "&f=" + viikonVikaPvm.ToShortDateString() + "&rooms=all");
            WilmaClassResourse kaikkiVaraukset = JsonConvert.DeserializeObject<WilmaClassResourse>(result2);

            foreach (var varaus in kaikkiVaraukset.RecourceSchedules)
            {
                //var vastaus = varaus.Schedule.GroupBy( z => z.Day).ToList();
                var vastaus = varaus.Schedule.GroupBy(z => new { z.Day, z.Groups.FirstOrDefault().Rooms }).ToList();
            }

            var classes = new List<string>(); int foundClass = 0; 
            var notFoundClasses = new List<string>();
            foreach (var varaus in kaikkiVaraukset.RecourceSchedules)
            {
                int luokanRuokailijat = 0;
                foreach (var aikataulu in varaus.Schedule)

                {
                    classes.Add(aikataulu.Class);
                    string[] split = aikataulu.Class.Split('/');
                    List<string> luokat = new List<string>();
                    int index = 0;
                    List<string> paikkakunnat = new List<string> { "HER", "PMT", "AMT", "HKK", "KER", "POM", "VAN", "ASK" };
                    foreach (string s in split)
                    {

                        if (CheckStarter(s, paikkakunnat))
                        {
                            if (split.Count() > (index + 1) && !CheckStarter(split[index + 1], paikkakunnat))
                            {
                                // Ensimmäisessä paikkakunta ja seuraavassa ei, muodostetaan niistä pari.
                                luokat.Add(s + split[index + 1]);
                            }
                        }
                        index++;

                    }
                    
                    // Suodatetaan pois iltaopetukset.
                    Tuple<int, int> alkaaKlo = ClockOp.GetClock(aikataulu.Start);
                    Tuple<int, int> loppuuKlo = ClockOp.GetClock(aikataulu.End);
                    // Jos tapahtuma alkaa klo 13 tai myöhemmin tai loppuu viimeistään klo 11 niin skipataan.
                    if (alkaaKlo.Item1 > 12 || loppuuKlo.Item1 < 12) continue;
                    if (loppuuKlo.Item1 > 16) continue;

                    var paikkakunta = aikataulu.Groups.FirstOrDefault().Rooms.FirstOrDefault().Caption.Substring(0, 3).ToUpper();

                    if (aikataulu.DateTimes.Count() > 1)
                    {
                        // Onko pvm useampi kuin yksi.
                        var datetimesit = aikataulu.DateTimes.Count();
                    }

                    DayOfWeek dayOfWeek = aikataulu.DateTimes.FirstOrDefault().DayOfWeek;

                    //int? studentsPresent = aikataulu?.Groups?.FirstOrDefault().StudentsPresent;
                    int studentsPresent = 0; int studentsPresentAll = 0;

                    // Selvitetään läsnäolijat.
                    aikataulu.Groups.ForEach(s => studentsPresent += s.StudentsPresent);
                    // Kaikki läsnäolijat. (myös ne joilla ei ole ruokaetua)
                    studentsPresentAll = studentsPresent;

                    // Selvitetään ruokailijat, jos ruokailijoita on vähemmän kuin läsnäolijoita, otetaan vain ruokailijamäärä.                  

                    // Haetaan azure storagesta tiedosto paikalliseksi.


                    PrimusOp primus = new PrimusOp("./");
                    List<EdupoliBizLib.Models.Primus.PrimusClass> primusClasses = primus.GetPrimusClasses();
                    
                    EdupoliBizLib.Models.Primus.PrimusClass r = (from a in primusClasses.OrderByDescending(b => b.name) where aikataulu.Class.Contains(a.name) select a).FirstOrDefault();
                    IEnumerable<EdupoliBizLib.Models.Primus.PrimusClass> rMany = (from a in primusClasses.OrderByDescending(b => b.name) where aikataulu.Class.Contains(a.name) select a);



                    if (rMany != null && rMany.Count() > 1)
                    {
                        Console.WriteLine(aikataulu.Class);

                        foreach (var x in rMany)
                        {
                            Console.WriteLine("Rmany:" + x.studentsFoodPermit);
                            luokanRuokailijat = luokanRuokailijat < x.studentsFoodPermit ? x.studentsFoodPermit : luokanRuokailijat;
                        }
                    }
                    else if (r != null)
                    {
                        if (studentsPresent >= r.studentsFoodPermit) luokanRuokailijat = r.studentsFoodPermit;
                    }
                    else
                    {
                        notFoundClasses.Add(aikataulu.Class);
                        luokanRuokailijat = studentsPresent;
                    }

                    string[] aikatauluClasses = aikataulu.Class.Split('/');
                    if (aikatauluClasses.Count() > 1)
                    {
                        Console.WriteLine(); Debug.Write("");
                        foreach (var ai in aikatauluClasses)
                        {
                            Console.Write(ai + ",");
                            Debug.Write(ai + ",");
                        }
                    }
            
                    if (studentsPresent >= luokanRuokailijat)
                    {
                        studentsPresent = luokanRuokailijat;                       
                        foundClass++;
                    }
                
                    switch (paikkakunta)
                    {
                        case "HKK":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].HKKryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HKK += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HKKall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HKKryhmat += aikataulu.Class + ", ";
                                model.AmirisHKKYhteensa += studentsPresent;
                                model.AmirisHKKYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();
                            }
                            break;
                        case "AMT":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].AMTryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].AMT += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].AMTall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].AMTryhmat += aikataulu.Class + ", ";
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();

                            }
                            break;
                        case "PMT":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].PMTryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].PMT += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].PMTall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].PMTryhmat += aikataulu.Class + ", ";
                                model.AmirisPMTYhteensa += studentsPresent;
                                model.AmirisPMTYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();
                            }
                            break;
                        case "POM":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].POMryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].POM += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].POMall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].POMryhmat += aikataulu.Class + ", ";
                                model.AmirisPOMYhteensa += studentsPresent;
                                model.AmirisPOMYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();
                            }
                            break;
                        case "ASK":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].ASKryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].ASK += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].ASKall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].ASKryhmat += aikataulu.Class + " " + studentsPresent + ", ";
                                model.AmirisASKYhteensa += studentsPresent;
                                model.AmirisASKYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();
                            }
                            break;
                        case "HER":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].HERryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HER += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HERall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].HERryhmat += aikataulu.Class + " " + studentsPresent + ", ";
                                model.AmirisHERYhteensa += studentsPresent;
                                model.AmirisHERYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();

                            }
                            break;
                        case "VAN":
                            if (!model.RuokalijaPaivaSaldo[(int)dayOfWeek].VANryhmat.Contains(aikataulu.Class))
                            {
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].VAN += studentsPresent;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].VANall += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].VANryhmat += aikataulu.Class + " " + studentsPresent + ", ";
                                model.AmirisVANYhteensa += studentsPresent;
                                model.AmirisVANYhteensaALL += studentsPresentAll;
                                model.RuokalijaPaivaSaldo[(int)dayOfWeek].Pvm = aikataulu.DateTimes.FirstOrDefault();

                            }
                            break;

                        default:
                            // Do nothing.
                            break;
                    }
                }
            }        
            
            StringBuilder sb = new StringBuilder();           
            
            // Poistetaan null arvot.
            model.RuokalijaPaivaSaldo = model.RuokalijaPaivaSaldo.Where(c => c != null).ToArray();
            // Poistetaan virhearvot PVM perusteella.
            model.RuokalijaPaivaSaldo = model.RuokalijaPaivaSaldo.Where(c => c.Pvm != DateTime.MinValue).ToArray();
            return model;

        }

        public List<object> CountRuokailijat2(string alkupvm, string paattyenpvm)
        {
                return (new List<object>
                {
                });
            }

        public ResurssiTilatModel PopulateTilat(string tyypit, string alkupvm, string paattyenpvm, string paikkak, string ajankohta, string resurssihaku)
        {
            {
                var vapaatHuoneet = new List<EdupoliBizLib.Models.Careeria.CareeriaRoom>();
                var varatutHuoneet = new List<EdupoliBizLib.Models.Careeria.CareeriaRoom>();
                var malli = new ResurssiTilatModel();
                DateTime alkupvmDate, paattyenpvmDate;

                bool muunnos = DateTime.TryParse(alkupvm, out alkupvmDate);
                muunnos = DateTime.TryParse(paattyenpvm, out paattyenpvmDate);
                if (!muunnos)
                {
                    paattyenpvmDate = alkupvmDate;
                    paattyenpvm = alkupvm;
                }
                // Haetaan kaikki tilat annettujen parametrien perusteella
                var result2 = wilma.Login("schedule/index_json?p=" + alkupvm + "&f=" + paattyenpvm + "&rooms=all");
                //var result2 = wilma.Login("schedule/index_json?p=" + alkupvm + "&f=" + paattyenpvm + "&rooms=all&teachers=all");
                // var result3 = wilma.Login("schedule/index_json?p=" + alkupvm + "&f=" + paattyenpvm + "&rooms=178&teachers=273");
                // Saatu tulos de-serialisoidaan poco-luokilla käsiteltäväksi tietorakenteeksi.
                WilmaClassResourse kaikkiVaraukset = JsonConvert.DeserializeObject<WilmaClassResourse>(result2);
                List<RecourceSchedule> tulema = (from a in kaikkiVaraukset.RecourceSchedules where a.Schedule.Count > 0 select a).ToList();
                List<RecourceSchedule> tulema2 = new List<RecourceSchedule>();

                foreach (RecourceSchedule x in tulema)
                {
                    RecourceSchedule newX = x;
                    foreach (var y in newX.Schedule)
                    {
                        var fixedTimes = new List<DateTime>();
                        foreach (DateTime z in y.DateTimes)
                        {
                            if (z == alkupvmDate || z == paattyenpvmDate) fixedTimes.Add(z);
                        }
                        y.DateTimes = fixedTimes;

                    }
                    if (newX.Schedule.Any(y => y.DateTimes.Count() > 0)) tulema2.Add(newX);
                }

                kaikkiVaraukset.RecourceSchedules = tulema2;
                List<CareeriaRoom> kaikkiHuoneet = new List<CareeriaRoom>();
                // Ladataan primus huoneet tiedosto



                using (StreamReader reader = new StreamReader(careeriaRoomJsonFileLocation))
                {
                    string line;
                    while ((line = reader.ReadLine()) != null)
                    {
                        kaikkiHuoneet = JsonConvert.DeserializeObject<List<CareeriaRoom>>(line);
                    }
                }

                List<CareeriaRoom.CareeriaPlace> halututPaikkakunnat = MuutaPaikkakunniksi(paikkak.ToUpper());
                List<CareeriaRoom.CareeriaType> halututTyypit = MuutaTyypeiksi(tyypit.ToUpper());

                // Nyt pitää selvittää kaikki ne huoneet, joissa kriteerit (huone ja tyyppi) täyttyvät
                List<CareeriaRoom> mahdollisetHuoneet = FilterPossibleRooms(kaikkiHuoneet, halututPaikkakunnat, halututTyypit);

                // Käydään kaikki varaukset läpi vs. kaikki mahdolliset huoneet 
                bool addAroom = false; int highwater = 0; int lowwater = 0;
                string shortcaption = String.Empty; string fullcaption = String.Empty; List<Teacher> teachers = new List<Teacher>();
                foreach (RecourceSchedule varaus in kaikkiVaraukset.RecourceSchedules)
                {
                    int StudentsPresent = 0;
                    if (mahdollisetHuoneet.Any(x => x.PrimusCardNumber == varaus.PrimusID))
                    {

                        // Huone löytyi, tarkistetaan, onko tapahtumia ajanjaksolle.
                        if (varaus.Schedule.Count() > 0)
                        {
                            addAroom = false; lowwater = 0; highwater = 0;
                            foreach (var sc in varaus.Schedule)
                            {
                                foreach (var d in sc.DateTimes)
                                {
                                    // Jos yksikin osuu tavoitellulle aikajaksolle niin lisätään huone varatuksi huoneeksi, muuten pidetään falsena.
                                    if (((alkupvmDate.Date == paattyenpvmDate.Date) && d == paattyenpvmDate) || (alkupvmDate.Date != paattyenpvmDate.Date && (d >= alkupvmDate && d <= paattyenpvmDate)))
                                    {
                                        if (!shortcaption.Contains(sc.Groups.FirstOrDefault().ShortCaption)) shortcaption += String.IsNullOrEmpty(shortcaption) ? sc.Groups.FirstOrDefault().ShortCaption : " / " + sc.Groups.FirstOrDefault().ShortCaption;
                                        if (!fullcaption.Contains(sc.Groups.FirstOrDefault().FullCaption)) fullcaption += String.IsNullOrEmpty(fullcaption) ? sc.Groups.FirstOrDefault().FullCaption : " / " + sc.Groups.FirstOrDefault().FullCaption;
                                        teachers = sc.Groups.FirstOrDefault().Teachers;
                                        Tuple<int, int> alkaaTpl = ClockOp.GetClock(sc.Start);
                                        Tuple<int, int> loppuuTpl = ClockOp.GetClock(sc.End);
                                        var loppuu = loppuuTpl.Item1;

                                        // Jos loppumisajankohta on yli puolen, lisätään loppumistuntia.
                                        if (loppuuTpl.Item2 > 0) loppuu++;

                                        // Haetaan varattuja tiloja
                                        if (resurssihaku == "varatut" || resurssihaku == "infotv")
                                        {
                                            switch (ajankohta)
                                            {
                                                case "kokop":
                                                    if (alkaaTpl.Item1 <= 12 && loppuu <= 16) addAroom = true;
                                                    break;
                                                case "aamup":
                                                    if (alkaaTpl.Item1 <= 12 && loppuu <= 12) addAroom = true;
                                                    break;
                                                case "iltap":
                                                    if (alkaaTpl.Item1 >= 12 && loppuu <= 16)
                                                    {
                                                        addAroom = true;
                                                    }
                                                    break;
                                                case "ilta":
                                                    if (alkaaTpl.Item1 >= 16) addAroom = true;
                                                    break;
                                                default:
                                                    addAroom = true;
                                                    break;
                                            }
                                        }
                                        // Haetaan vapaita tiloja
                                        else
                                        {
                                            switch (ajankohta)
                                            {
                                                case "kokop":
                                                    if (loppuu <= 16 || (alkaaTpl.Item1 <= 16 && loppuu >= 16)) addAroom = true;
                                                    break;
                                                case "aamup":
                                                    if (alkaaTpl.Item1 <= 12) addAroom = true;
                                                    break;
                                                case "iltap":
                                                    if ((alkaaTpl.Item1 > 11 && loppuu < 17) || (alkaaTpl.Item1 > 7 && (loppuu > 12 && loppuu < 17)) || (alkaaTpl.Item1 > 7 && loppuu > 16))
                                                    {
                                                        addAroom = true;
                                                    }
                                                    break;
                                                case "ilta":
                                                    if (alkaaTpl.Item1 >= 16 || (alkaaTpl.Item1 <= 16 && loppuu > 16)) addAroom = true;
                                                    break;
                                                default:
                                                    addAroom = true;
                                                    break;
                                            }
                                        }


                                        //  HighWater, Lowwater - etsitään koko päivän varaus
                                        if (lowwater >= alkaaTpl.Item1 && lowwater != 0) lowwater = alkaaTpl.Item1;
                                        else if (lowwater == 0) lowwater = alkaaTpl.Item1;
                                        if (highwater < loppuu && highwater != 0) highwater = loppuu;
                                        else if (highwater == 0) highwater = loppuu;
                                        //StudentsPresent += sc.Groups.FirstOrDefault().StudentsPresent;
                                    }
                                }
                                StudentsPresent = StudentsPresent < sc.Groups.FirstOrDefault().StudentsPresent ? sc.Groups.FirstOrDefault().StudentsPresent : StudentsPresent;
                            }

                            if (addAroom)
                            {
                                CareeriaRoom room = (from a in kaikkiHuoneet where a.PrimusCardNumber == varaus.PrimusID select a).FirstOrDefault();                                
                                room.ShortCaprtion = shortcaption;
                                room.FullCaption = fullcaption;
                                shortcaption = String.Empty;
                                fullcaption = String.Empty;
                                room.Teachers = teachers;
                                if (lowwater > 0) room.StartTime = DateTime.Now.Date.AddHours(lowwater);
                                if (highwater > 0) room.EndTime = DateTime.Now.Date.AddHours(highwater);
                                room.StudentsPresent += StudentsPresent;
                                varatutHuoneet.Add(room);
                                StudentsPresent = 0;

                            }
                        }
                    }
                }

                if (resurssihaku == "infotv") malli.ShowInInfoTVFormat = true;
                switch (resurssihaku)
                {
                    case "varatut":
                    case "infotv":
                        malli.ShowPeriod = true;
                        malli.ResurssiHuoneet = varatutHuoneet;
                        break;
                    case "ruokailijat":
                        break;
                    default:
                        foreach (var huone in mahdollisetHuoneet)
                        {
                            if (varatutHuoneet.Any(x => x.PrimusCardNumber == huone.PrimusCardNumber)) { }
                            else
                            {
                                // Huoneeseen ei ole varausta, joten lisätään.
                                CareeriaRoom lisattava = (from a in kaikkiHuoneet where a.PrimusCardNumber == huone.PrimusCardNumber select a).FirstOrDefault();
                                vapaatHuoneet.Add(lisattava);
                            }
                        }
                        malli.ResurssiHuoneet = vapaatHuoneet;
                        malli.ShowPeriod = false;
                        break;
                }
                malli.ResurssiHuoneet = malli.ResurssiHuoneet.OrderBy(x => x.StartTime).ThenBy(y => y.EndTime).ToList();
                return malli;
            }
        }

        private bool CheckStarter(string s, List<string> starters)
        {
            foreach (string start in starters)
            {
                if (s.StartsWith(start)) return true;
            }
            return false;
        }

        private List<CareeriaRoom> FilterPossibleRooms(List<CareeriaRoom> kaikkiHuoneet, List<CareeriaRoom.CareeriaPlace> halututPaikkakunnat, List<CareeriaRoom.CareeriaType> halututTyypit)
        {
            var filteredRooms = new List<CareeriaRoom>();
            bool includePaikkakunta = false, includeType = false;
            foreach (CareeriaRoom huone in kaikkiHuoneet)
            {
                if (halututPaikkakunnat.Any(x => x == huone.Place)) includePaikkakunta = true;
                if (halututTyypit.Any(y => y == huone.Type)) includeType = true;
                if (includePaikkakunta && includeType)
                {
                    // Tarkistetaan, ettei ole ennestään
                    if (!(filteredRooms.Any(x => x.Name == huone.Name))) filteredRooms.Add(huone);

                }
                includePaikkakunta = false; includeType = false;
            }
            return filteredRooms;
        }

        private List<CareeriaRoom.CareeriaPlace> MuutaPaikkakunniksi(string paikkak)
        {
            var places = new List<CareeriaRoom.CareeriaPlace>();
            for (int i = 0; i < paikkak.Length; i = i + 3)
            {
                string paikka = paikkak.Substring(i, 3);
                CareeriaRoom.CareeriaPlace paikkakunta = CareeriaSpesific.ParsePlace(paikka).Place;
                places.Add(paikkakunta);

            }
            return places;
        }

        private List<CareeriaRoom.CareeriaType> MuutaTyypeiksi(string tyypit)
        {
            var types = new List<CareeriaRoom.CareeriaType>();
            for (int i = 0; i < tyypit.Length; i = i + 3)
            {
                string tyyppi = tyypit.Substring(i, 3);
                CareeriaRoom.CareeriaType tyyp = CareeriaSpesific.ParseType(tyyppi).Type;
                types.Add(tyyp);
            }
            return types;
        }
    }
}
