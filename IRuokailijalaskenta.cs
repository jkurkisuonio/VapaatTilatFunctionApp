using EdupoliBizLib.Models.Careeria;
using System.Collections.Generic;

namespace VapaatTilatFunctionApp
{
    internal interface IWilmaResurssiLaskenta
    {
        ResurssiTilatModel CountRuokailijat(string alkupvm, string paattyenpvm);
        ResurssiTilatModel PopulateTilat(string tyypit, string alkupvm, string paattyenpvm, string paikkak, string ajankohta, string resurssihaku);
        List<object> CountRuokailijat2(string alkupvm, string paattyenpvm);
    }
}