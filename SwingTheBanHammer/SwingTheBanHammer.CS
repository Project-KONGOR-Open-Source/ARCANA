using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;

Console.Write("Your KONGOR Portal User Name: ");
string username = Console.ReadLine() ?? string.Empty;

Console.Write("Your KONGOR Portal Password: ");

string password = string.Empty;

ConsoleKey key;

do
{
    ConsoleKeyInfo keyInfo = Console.ReadKey(true);

    key = keyInfo.Key;

    if (key == ConsoleKey.Backspace && password.Length > 0)
    {
        Console.Write("\b \b");
        password = password[..^1];
    }

    else if (!char.IsControl(keyInfo.KeyChar))
    {
        Console.Write("*");
        password += keyInfo.KeyChar;
    }

} while (key != ConsoleKey.Enter);

Console.WriteLine();

HttpClient client = new()
{
    BaseAddress = new Uri("https://api.kongor.online/")
};

HttpResponseMessage loginResponse = await client.PostAsJsonAsync("identities/login", new Dictionary<string, string> { { "name", username }, { "password", password } });

Console.WriteLine();

string token = string.Empty;

if (loginResponse.IsSuccessStatusCode)
{
    Console.WriteLine("Logged In Successfully");
    token = ((await loginResponse.Content.ReadFromJsonAsync<Dictionary<string, object>>()) ?? new Dictionary<string, object> { { "token", string.Empty } })["token"].ToString() ?? string.Empty;
}

else
{
    Console.WriteLine(await loginResponse.Content.ReadAsStringAsync());
    Environment.Exit(0);
}

if (string.IsNullOrWhiteSpace(token))
{
    Console.WriteLine("Invalid Login JWT");
    Environment.Exit(0);
}

client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

bool more = true;

while (more)
{
    Console.WriteLine();

    Console.Write("Offender Account Name: ");
    string offender = Console.ReadLine() ?? string.Empty;

    Console.Write("Reason For Suspension: ");
    string reason = Console.ReadLine() ?? string.Empty;

    Console.Write("Duration Of Suspension (e.g. 4d, 3w, 2m, 1y): ");
    string duration = Console.ReadLine() ?? string.Empty;

    Console.WriteLine();

    HttpResponseMessage suspensionResponse = await client.PostAsJsonAsync("suspensions", new Dictionary<string, string> { { "accountName", offender }, { "duration", duration }, { "reason", reason } });

    if (new[] { HttpStatusCode.Unauthorized, HttpStatusCode.Forbidden }.Contains(suspensionResponse.StatusCode))
    {
        Console.WriteLine($@"Account ""{username}"" Is Not Authorised To Issue Suspensions");
        Environment.Exit(0);
    }

    if (suspensionResponse.IsSuccessStatusCode) Console.WriteLine("Suspension Issued Successfully");
    else Console.WriteLine(await suspensionResponse.Content.ReadAsStringAsync());

    Console.WriteLine();

    Console.Write("Swing The Ban Hammer More? (Y/N): ");
    more = Console.ReadKey().KeyChar.ToString().ToUpper() is "Y";

    Console.WriteLine();
}
