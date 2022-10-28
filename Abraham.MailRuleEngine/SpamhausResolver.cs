using DnsClient;
using DnsClient.Protocol;
using System.Net;
using System.Net.Sockets;
using System.Text.RegularExpressions;
using Tireless.Net.Blocking;

namespace Abraham.MailRuleEngine;

/// <summary>
/// Class to query popular DNS based spam blacklists, to get info about ip addresses.
/// This source code was written by Marlin Brüggemann.
/// https://github.com/mabru47/Spamhaus.Net
/// It was originally published under MIT license.
/// </summary>
public class SpamhausResolver
{
    #region ------------- Properties ----------------------------------------------------------
    public IPAddress[] SpamhausNameserver
    {
        get { return UseIPv6 && _spamhausNameserverV6.Length > 0 ? _spamhausNameserverV6 : _spamhausNameserverV4; }
    }

    /// <summary>
    /// Use ipv6 instead of ipv4.
    /// </summary>
    public bool UseIPv6 { get; set; }

    /// <summary>
    /// Cache all results.
    /// </summary>
    public bool UseCache { get; set; }

    /// <summary>
    /// No exceptions are thrown.
    /// </summary>
    public bool QuietMode { get; set; }

    public IPAddress[] NameserverV4 { get; set; }

    public IPAddress[] NameserverV6 { get; set; }

    public TimeSpan QueryTimeout { get; set; } 
    #endregion



    #region ------------- Fields --------------------------------------------------------------
    private IPTree _blockTree;
    private IPAddress[] _spamhausNameserverV4;
    private IPAddress[] _spamhausNameserverV6;
    #endregion



    #region ------------- Init ----------------------------------------------------------------
    public SpamhausResolver()
    {
        _blockTree = new IPTree();

        NameserverV4 = new IPAddress[] { IPAddress.Parse("8.8.8.8"), IPAddress.Parse("8.8.4.4") };
        NameserverV6 = new IPAddress[] { IPAddress.Parse("2001:4860:4860::8888"), IPAddress.Parse("2001:4860:4860::8844") };

        _spamhausNameserverV4 = new IPAddress[0];
        _spamhausNameserverV6 = new IPAddress[0];

        UseCache = true;
        QueryTimeout = TimeSpan.FromSeconds(10);
    }
    #endregion



    #region ------------- Methods -------------------------------------------------------------
    public SpamhausResolver UseQueryTimeout(TimeSpan queryTimeout)
    {
        QueryTimeout = queryTimeout;
        return this;
    }

    public SpamhausResolver Initialize()
    {
        InitializeAsync().GetAwaiter().GetResult();
        return this;
    }

    public async Task InitializeAsync(TimeSpan? timeout = null)
    {
        try
        {
            var lookup = new LookupClient(NameserverV4)
            {
                UseCache = false,
                Timeout = QueryTimeout,
            };

            var nsLookupResult = await lookup.QueryAsync("zen.spamhaus.org", QueryType.NS);
            if (nsLookupResult.Answers.Count > 0)
            {
                var nsRndResult = nsLookupResult.Answers.ElementAt((int)(DateTime.UtcNow.Ticks % nsLookupResult.Answers.Count));

                if (nsRndResult is NsRecord nsRecord)
                {
                    var hostV4List = new List<IPAddress>();
                    var hostV6List = new List<IPAddress>();
                    var hostEntryResult = await lookup.GetHostEntryAsync(nsRecord.NSDName);
                    foreach (var item in hostEntryResult.AddressList)
                    {
                        if (item.AddressFamily == AddressFamily.InterNetwork)
                            hostV4List.Add(item);
                        else if (item.AddressFamily == AddressFamily.InterNetworkV6)
                            hostV6List.Add(item);
                    }
                    _spamhausNameserverV4 = hostV4List.ToArray();
                    _spamhausNameserverV6 = hostV6List.ToArray();
                }
            }
        }
        catch (Exception)
        {
            if (QuietMode == false)
                throw;
        }
    }

    public async Task AddUrlAsync(string url)
    {
        try
        {
            using (var httpClient = new HttpClient())
            {
                using (var httpStream = await httpClient.GetStreamAsync(url))
                {
                    await AddStreamAsync(httpStream);
                }
            }
        }
        catch (Exception)
        {
            if (QuietMode == false)
                throw;
        }
    }

    public async Task AddFileAsync(string path)
    {
        try
        {
            using (var fileStream = File.OpenRead(path))
            {
                await AddStreamAsync(fileStream);
            }
        }
        catch (Exception)
        {
            if (QuietMode == false)
                throw;
        }
    }

    public async Task AddStreamAsync(Stream baseStream)
    {
        try
        {
            using (var sr = new StreamReader(baseStream))
            {
                string? line;
                while ((line = await sr.ReadLineAsync()) != null)
                {
                    if (line.Length == 0 || line[0] == ';')
                        continue;

                    var regex = new Regex(@"^(.*)\/([0-9]+) ; (.*)");
                    var matches = regex.Matches(line);
                    if (matches.Count > 0)
                    {
                        var ip = IPAddress.Parse(matches[0].Groups[1].Value);
                        var netmask = Byte.Parse(matches[0].Groups[2].Value);
                        var ident = matches[0].Groups[3].Value;

                        _blockTree.AddNetwork(ip, netmask, ident);
                    }
                }
            }
        }
        catch (Exception)
        {
            if (QuietMode == false)
                throw;
        }
    }

    public void AddNetwork(IPAddress network, Int32 mask, string identifier = "")
    {
        _blockTree.AddNetwork(network, mask, identifier);
    }

    public void AddIPAddress(IPAddress client, string identifier = "")
    {
        _blockTree.AddNetwork(client, client.GetAddressBytes().Length * 8, identifier);
    }

    /// <summary>
    /// Calls also InitSpamhausNameservers if not happened before.
    /// </summary>
    /// <param name="client"></param>
    /// <param name="timeout"></param>
    /// <returns></returns>
    public async Task<string?> IsBlockedAsync(IPAddress client)
    {
        try
        {
            if (UseCache)
            {
                string identifier;
                if ((identifier = _blockTree.IsBlocked(client)) != null)
                {
                    if (identifier != SpamhausResult.NL.ToString())
                        return identifier;
                    return null;
                }
            }

            if (SpamhausNameserver.Length == 0)
                await InitializeAsync();

            if (SpamhausNameserver.Length == 0)
                return null;

            var lookup = new LookupClient(SpamhausNameserver)
            {
                UseCache = false,
                Timeout = QueryTimeout,
            };

            string reverseIPAddress;
            var ipBytes = new List<Byte>(client.GetAddressBytes()).Reverse<Byte>();
            if (client.AddressFamily == AddressFamily.InterNetwork)
            {
                reverseIPAddress = string.Join(".", ipBytes);
            }
            else if (client.AddressFamily == AddressFamily.InterNetworkV6)
            {
                reverseIPAddress = "";
                foreach (var item in ipBytes.ToArray())
                {
                    var hex = item.ToString("X2");
                    reverseIPAddress += "." + hex[1] + "." + hex[0];
                }
                reverseIPAddress = reverseIPAddress.Substring(1);
            }
            else
                throw new NotSupportedException();

            var lookupResult = await lookup.QueryAsync(reverseIPAddress.ToLowerInvariant() + ".zen.spamhaus.org", QueryType.A);

            SpamhausResult spamhausResult = SpamhausResult.NL;
            foreach (var item in lookupResult.Answers)
            {
                if (item is AddressRecord addressRecord)
                {
                    spamhausResult |= (SpamhausResult)(addressRecord.Address.GetAddressBytes()[3]);
                }
            }

            if (UseCache)
            {
                _blockTree.AddIPAddress(client, spamhausResult.ToString());
            }

            return spamhausResult != SpamhausResult.NL ? spamhausResult.ToString() : null;

        }
        catch (Exception)
        {
            if (QuietMode == false)
                throw;
            return null;
        }
    }
    #endregion



    #region ------------- Implementation ------------------------------------------------------
    #endregion
}

