using Microsoft.Data.SqlClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Data.SqlClient;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Net.Mail;
using System.Net.NetworkInformation;
using System.Net.Security;
using System.Runtime.Intrinsics.X86;
using System.Security.Cryptography.X509Certificates;
using System.Text.RegularExpressions;
 
public static class GenericMethods
{
	public static int UserID;

	private static Regex ValidEmailRegex = CreateValidEmailRegex();

	public static byte[] Decompress(byte[] compressedData)
	{
		using MemoryStream stream = new MemoryStream(compressedData);
		using MemoryStream memoryStream = new MemoryStream();
		using (DeflateStream deflateStream = new DeflateStream(stream, CompressionMode.Decompress))
		{
			deflateStream.CopyTo(memoryStream);
		}
		return memoryStream.ToArray();
	}


	public static bool BooleanConveter(string val)
	{
		if (val.TrimStart().TrimEnd() == "Active")
		{
			return true;
		}
		return false;
	}

    public static bool? GetNullableBool(SqlDataReader reader, string columnName)
    {
        var value = reader[columnName];
        if (value == DBNull.Value)
            return null;

        // Handle different types that might represent boolean
        if (value is bool)
            return (bool)value;

        if (value is int || value is short || value is long)
            return Convert.ToInt32(value) == 1;

        if (value is string stringValue)
            return stringValue.Trim() == "1" || stringValue.Trim().ToLower() == "true";

        return Convert.ToBoolean(value);
    }

    public static string GetIP()
	{
		string text = "";
		text = Dns.GetHostName();
		IPHostEntry hostEntry = Dns.GetHostEntry(text);
		return hostEntry.AddressList[^1].ToString();
	}

	public static bool BooleanConveterNumber(string val)
	{
		if (val.TrimStart().TrimEnd() == "1")
		{
			return true;
		}
		return false;
	}

	public static string BooleanConveterNumberText(string val)
	{
		if (val.TrimStart().TrimEnd() == "1")
		{
			return "Yes";
		}
		return "No";
	}

	public static object Isnull(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return value = 0;
		}
		return value;
	}

	public static object IsnullString(string value)
	{
		if (value == string.Empty || value == null || value == "&nbsp;")
		{
			return value = string.Empty;
		}
		return value;
	}

	public static object IsnullString_Object(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return value = string.Empty;
		}
		return value;
	}

	public static object IsnullDate(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return value = DateTime.Now;
		}
		return value;
	}

	public static object IsnullDateYear(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return value = DateTime.Now.Year;
		}
		return DateTime.Now.Year;
	}

	public static int Isnull_Integer(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return 0;
		}
		return Convert.ToInt32(value);
	}

	public static decimal Isnull_Decimal(object value)
	{
		if (value.ToString() == string.Empty || value == null)
		{
			return 0m;
		}
		return Convert.ToDecimal(value);
	}

	public static bool IsNumber(string value)
	{
		if (double.TryParse(value.Replace("-", string.Empty).TrimStart(), out var _))
		{
			return true;
		}
		return false;
	}

	public static bool IsCorrectScore(string value)
	{
		if (double.TryParse(value.Replace("-", string.Empty).TrimStart(), out var _))
		{
			return value switch
			{
				"1" => true, 
				"3" => true, 
				"5" => true, 
				"0" => true, 
				_ => false, 
			};
		}
		return false;
	}

	public static bool IsVisible(string value)
	{
		if (value != string.Empty)
		{
			return true;
		}
		return false;
	}

	public static object CheckNegative(object value)
	{
		if (value.ToString().Substring(0, 1) == "-")
		{
			decimal num = Convert.ToDecimal(value.ToString().Replace("-", "").Trim());
			return value = num - num;
		}
		return value;
	}

	public static byte[] StreamToByteArray(Stream input)
	{
		byte[] array = new byte[16384];
		using MemoryStream memoryStream = new MemoryStream();
		int count;
		while ((count = input.Read(array, 0, array.Length)) > 0)
		{
			memoryStream.Write(array, 0, count);
		}
		return memoryStream.ToArray();
	}

	public static bool CheckForInternetConnection()
	{
		if (!NetworkInterface.GetIsNetworkAvailable())
		{
			return false;
		}
		return true;
	}

	public static Color HeatMapColor(string Colour)
	{
		return Colour switch
		{
			"Critical" => Color.Red, 
			"High" => Color.Orange, 
			"Moderate" => Color.Yellow, 
			"Low" => Color.Lime, 
			"Minor" => Color.Green, 
			_ => Color.White, 
		};
	}

	private static Regex CreateValidEmailRegex()
	{
		string pattern = "^(?!\\.)(\"([^\"\\r\\\\]|\\\\[\"\\r\\\\])*\"|([-a-z0-9!#$%&'*+/=?^_`{|}~]|(?<!\\.)\\.)*)(?<!\\.)@[a-z0-9][\\w\\.-]*[a-z0-9]\\.[a-z][a-z\\.]*[a-z]$";
		return new Regex(pattern, RegexOptions.IgnoreCase);
	}

	public static bool EmailIsValid(string emailAddress)
	{
		return ValidEmailRegex.IsMatch(emailAddress);
	}
    public static int? GetInt32OrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetInt32(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as int: {ex.Message}");
            return null;
        }
    }

    public static DateTime? GetDateTimeOrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDateTime(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as DateTime: {ex.Message}");
            return null;
        }
    }

    public static bool? GetBooleanOrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetBoolean(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as bool: {ex.Message}");
            return null;
        }
    }

    public static string GetStringOrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as string: {ex.Message}");
            return null;
        }
    }

    public static double? GetDoubleOrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetDouble(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as double: {ex.Message}");
            return null;
        }
    }

    public static float? GetFloatOrNull(this IDataReader reader, string columnName)
    {
        try
        {
            int ordinal = reader.GetOrdinal(columnName);
            return reader.IsDBNull(ordinal) ? null : reader.GetFloat(ordinal);
        }
        catch (IndexOutOfRangeException)
        {
            return null; // Column not found
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading {columnName} as float: {ex.Message}");
            return null;
        }
    }

    public static bool HasColumn(this IDataReader reader, string columnName)
    {
        for (int i = 0; i < reader.FieldCount; i++)
        {
            if (reader.GetName(i).Equals(columnName, StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }


    public static void LogError(string ERROR_DESCRIPTION, string ERROR_MODULE, Guid ERROR_USER_ID)
	{
	}

    public static async Task SendEmail_Office365Async(string to, string subject, string body)
    {
        try
        {
            using (SmtpClient smtpClient = new SmtpClient("smtp.office365.com", 587))
            using (MailMessage mailMessage = new MailMessage())
            {
                mailMessage.From = new MailAddress("Ithuba@TSHWANE.GOV.ZA");
                mailMessage.To.Add(new MailAddress(to));
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                smtpClient.EnableSsl = true;
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential("Ithuba@TSHWANE.GOV.ZA", "Qf9!tZ3p@Lw8");
                smtpClient.Timeout = 60000;

                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"Email sent successfully to: {to}");


                //using (SmtpClient smtpClient = new SmtpClient("smtp.office365.com", 587))
                //using (MailMessage mailMessage = new MailMessage())
                //{
                //    mailMessage.From = new MailAddress("BCR@TSHWANE.GOV.ZA");
                //    mailMessage.To.Add(new MailAddress(to));
                //    mailMessage.Subject = subject;
                //    mailMessage.Body = body;
                //    mailMessage.IsBodyHtml = true;

                //    smtpClient.EnableSsl = true;
                //    smtpClient.UseDefaultCredentials = false;
                //    smtpClient.Credentials = new NetworkCredential("BCR@TSHWANE.GOV.ZA", "T$hwane12345");
                //    smtpClient.Timeout = 60000;

                //    await smtpClient.SendMailAsync(mailMessage);
                //    Console.WriteLine($"Email sent successfully to: {to}");
                }
            }
        catch (SmtpException ex)
        {
            throw new Exception($"SMTP Error sending email to {to}: {ex.Message}", ex);
        }
    }

    public static async Task SendEmailAsync(string to, string subject, string body)
    {
        try
        {
            using (SmtpClient smtpClient = new SmtpClient("smtp.office365.com", 587))
            using (MailMessage mailMessage = new MailMessage())
            {
                mailMessage.From = new MailAddress("Ithuba@TSHWANE.GOV.ZA");
                mailMessage.To.Add(new MailAddress(to));
                mailMessage.Subject = subject;
                mailMessage.Body = body;
                mailMessage.IsBodyHtml = true;

                smtpClient.EnableSsl = true;
                smtpClient.UseDefaultCredentials = false;
                smtpClient.Credentials = new NetworkCredential("Ithuba@TSHWANE.GOV.ZA", "#ICT*support#");
                smtpClient.Timeout = 60000;

                await smtpClient.SendMailAsync(mailMessage);
                Console.WriteLine($"Email sent successfully to: {to}");
            }
        }
        catch (SmtpException ex)
        {
            throw new Exception($"SMTP Error sending email to {to}: {ex.Message}", ex);
        }
    }


    public static string BuildHtmlPasswordRecovery(string Fullname, string Username, string Password)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Password Recovery – Ithuba Youth Economic Development Programme</title>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; background:#f6f6f6; margin:0; padding:20px 0; }}
        .container {{ max-width:650px; margin:0 auto; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.08); }}
        .header {{ background:linear-gradient(135deg, #49A942, #367a30); color:white; padding:50px 20px; text-align:center; }}
        .header h1 {{ margin:0; font-size:32px; }}
        .header p {{ margin:12px 0 0; font-size:18px; opacity:0.95; }}
        .content {{ padding:40px 35px; line-height:1.8; color:#2c3e50; }}
        .highlight {{ color:#49A942; font-weight:bold; }}
        .btn {{ display:inline-block; background:#49A942; color:white; padding:16px 40px; text-decoration:none; border-radius:8px; font-weight:bold; font-size:17px; box-shadow:0 4px 15px rgba(73,169,66,0.3); }}
        .footer {{ background:#f9f9f9; padding:30px; text-align:center; font-size:12px; color:#666; border-top:1px solid #e0e0e0; }}
    </style>
</head>
<body>
    <div class='container'>

        <!-- Header -->
        <div class='header'>
            <h1>Ithuba Youth Economic Development Programme</h1>
            <p>Planting a seed, empowering our youth and building a great city</p>
        </div>

        <!-- Main Content -->
        <div class='content'>
            <h2 style='color:#49A942; text-align:center;'>Password Recovery</h2>

            <p>Dear <span class='highlight'>{Fullname}</span>,</p>

            <p>We have received a request to recover your account credentials. Below are your login details:</p>

            <ul style='background:#f8fff8; padding:25px; border-left:5px solid #49A942; border-radius:8px; margin:30px 0; font-size:16px;'>
                <li><strong>Username:</strong> {Username}</li>
                <li><strong>Password:</strong> {Password}</li>
            </ul>

            <p style='text-align:center; margin:35px 0;'>
                <a href='https://iyedp.tshwane.gov.za/app-login' class='btn'>Log In to Your Portal Now</a>
            </p>

            <p>Please use these credentials to log in to your account. For security reasons, we recommend changing your password after logging in.</p>

            <p>If you did not request this recovery, please contact our support team immediately:</p>
            <p style='background:#fff8e1; padding:15px; border-radius:6px; text-align:center; font-size:15px;'>
                Email: <a href='mailto:Ithuba@TSHWANE.GOV.ZA'>ithuba@tshwane.gov.za</a><br>
                Tel: <strong>012 358 1634 / 5700 / 5587</strong>
            </p>
        </div>

        <!-- Footer with full contact details -->
        <div class='footer'>
            <p><strong>City of Tshwane • Economic Development Division</strong><br>
            6th Floor, Middestad Building, 252 Thabo Sehume Street, Pretoria, 0002<br>
            PO Box 6338, Pretoria, 0001</p>
            <p>Tel: 012 358 1634 / 5700 / 5587 | Email: <a href='mailto:Ithuba@TSHWANE.GOV.ZA'>ithuba@tshwane.gov.za</a></p>
            <p>© {DateTime.Now.Year} City of Tshwane. All rights reserved.</p>
            <p style='color:#999; font-size:11px;'>This is an automated message from the Ithuba Youth Portal. Please do not reply.</p>
        </div>
    </div>
</body>
</html>";
    }




    public static string BuildHtmlIthubaWelcome(string Fullname)
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Welcome to Ithuba Youth Economic Development Programme</title>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; background:#f6f6f6; margin:0; padding:20px 0; }}
        .container {{ max-width:650px; margin:0 auto; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.08); }}
        .header {{ background:linear-gradient(135deg, #49A942, #367a30); color:white; padding:50px 20px; text-align:center; }}
        .header h1 {{ margin:0; font-size:32px; }}
        .header p {{ margin:12px 0 0; font-size:18px; opacity:0.95; }}
        .content {{ padding:40px 35px; line-height:1.8; color:#2c3e50; }}
        .highlight {{ color:#49A942; font-weight:bold; }}
        .quote {{ text-align:center; font-style:italic; color:#555; background:#f0f7ef; padding:30px; border-radius:10px; margin:35px 0; font-size:17px; }}
        .important-note {{ background:linear-gradient(135deg, #fff3cd, #ffeeba); border-left:6px solid #ffc107; padding:25px 30px; margin:35px 0; border-radius:8px; }}
        .important-note .nb-badge {{ display:inline-block; background:#ffc107; color:#000; font-weight:bold; font-size:14px; padding:4px 12px; border-radius:4px; margin-bottom:12px; text-transform:uppercase; letter-spacing:1px; }}
        .important-note p {{ margin:0; color:#856404; font-size:16px; line-height:1.6; }}
        .footer {{ background:#f9f9f9; padding:30px; text-align:center; font-size:12px; color:#666; border-top:1px solid #e0e0e0; }}
    </style>
</head>
<body>
    <div class='container'>

        <!-- Header -->
        <div class='header'>
            <h1>Ithuba Youth Economic Development Programme</h1>
            <p>Planting a seed, empowering our youth and building a great city</p>
        </div>

        <!-- Main Content -->
        <div class='content'>
            <h2 style='color:#49A942; text-align:center;'>Sawubona <span class='highlight'>{Fullname}</span>!</h2>

            <p style='font-size:18px; text-align:center; margin:25px 0;'>
                <strong>Welcome to the Ithuba family!</strong>
            </p>

            <p>You have just taken a powerful first step. By registering for the <strong>Ithuba Youth Economic Development Programme</strong>, you are now part of a movement that is creating real opportunities for young people across the City of Tshwane.</p>

            <div class='quote'>
                ""Be the change you wish to see in the world.""<br>
                <strong>— Mahatma Gandhi</strong>
            </div>

            <p style='font-size:17px;'>Together we will help you:</p>
            <ul style='font-size:16px; padding-left:25px;'>
                <li>Start or grow your own business</li>
                <li>Gain skills that employers and markets actually need</li>
                <li>Access jobs, internships, and apprenticeships</li>
                <li>Connect with markets and procurement opportunities</li>
                <li>Get your driver's licence and increase your mobility</li>
                <li>Be celebrated through the Tshwane Youth Awards</li>
            </ul>

            <div class='important-note'>
                <span class='nb-badge'>NB</span>
                <p><strong>Important note:</strong><br>
                The Ithuba Team will be in touch with you for further screening, selection, and scheduling. Please keep checking your emails, SMS, and WhatsApp messages for further communication.</p>
            </div>

            <p>We are so excited to walk this road with you. Your dreams matter. Your future starts <strong>today</strong>.</p>

            <p style='text-align:center; color:#49A942; font-size:18px; margin-top:40px;'>
                <strong>Team Ithuba – City of Tshwane</strong>
            </p>
        </div>

        <!-- Footer -->
        <div class='footer'>
            <p><strong>City of Tshwane • Economic Development Division</strong><br>
            6th Floor, Middestad Building, 252 Thabo Sehume Street, Pretoria, 0002<br>
            PO Box 6338, Pretoria, 0001</p>
            <p>Tel: 012 358 1634 / 5700 / 5587 | Email: <a href='mailto:Ithuba@TSHWANE.GOV.ZA'>ithuba@tshwane.gov.za</a></p>
            <p>© {DateTime.Now.Year} City of Tshwane. All rights reserved.</p>
            <p style='color:#999; font-size:11px;'>This is an automated welcome message from the Ithuba Youth Programme.</p>
        </div>
    </div>
</body>
</html>";
    }



    public static string BuildHtmlCompanyPledgeThankYou(
    string CompanyName
 )  // Optional: "Sponsor", "Partner", "Funder", etc.
    {
        return $@"
<!DOCTYPE html>
<html lang='en'>
<head>
    <meta charset='UTF-8'>
    <meta name='viewport' content='width=device-width, initial-scale=1.0'>
    <title>Thank You for Powering Ithuba – City of Tshwane</title>
    <style>
        body {{ font-family: Arial, Helvetica, sans-serif; background:#f6f6f6; margin:0; padding:20px 0; }}
        .container {{ max-width:680px; margin:0 auto; background:#ffffff; border-radius:12px; overflow:hidden; box-shadow:0 10px 30px rgba(0,0,0,0.08); }}
        .header {{ background:linear-gradient(135deg, #49A942, #367a30); color:white; padding:50px 20px; text-align:center; }}
        .header h1 {{ margin:0; font-size:34px; }}
        .header p {{ margin:15px 0 0; font-size:19px; opacity:0.95; }}
        .content {{ padding:45px 40px; line-height:1.8; color:#2c3e50; }}
        .highlight {{ color:#49A942; font-weight:bold; }}
        .pledge-box {{ background:#f8fff8; border:2px solid #49A942; border-radius:12px; padding:30px; text-align:center; margin:35px 0; }}
        .pledge-box h3 {{ margin:0 0 15px 0; color:#49A942; font-size:24px; }}
        .pledge-box p {{ margin:8px 0; font-size:18px; }}
        .btn {{ display:inline-block; background:#49A942; color:white; padding:18px 45px; text-decoration:none; border-radius:8px; font-weight:bold; font-size:18px; box-shadow:0 6px 20px rgba(73,169,66,0.3); }}
        .impact {{ background:#f0f7ef; padding:30px; border-radius:10px; margin:40px 0; text-align:center; font-size:17px; }}
        .footer {{ background:#f9f9f9; padding:35px; text-align:center; font-size:12px; color:#666; border-top:1px solid #e0e0e0; }}
    </style>
</head>
<body>
    <div class='container'>

        <!-- Header -->
        <div class='header'>
            <h1>Ithuba Youth Economic Development Programme</h1>
            <p>Planting a seed, empowering our youth and building a great city</p>
        </div>

        <!-- Main Content -->
        <div class='content'>
            <h2 style='color:#49A942; text-align:center; font-size:28px;'>
                Thank You, <span class='highlight'>{CompanyName}</span>!
            </h2>

            <p style='text-align:center; font-size:19px;'>
                Your pledge is making the Ithuba initiative a powerful success.
            </p>
 

            <div class='impact'>
                <p><strong>Because of partners like you:</strong></p>
                <p>Thousands of young people in Tshwane will gain skills, start businesses,<br>
                access jobs, markets, and build a future they can be proud of.</p>
            </div>

            <p style='text-align:center;'>
                <strong>Your support is not just a pledge — it is hope in action.</strong>
            </p>

            <p style='text-align:center; margin:40px 0;'>
                <a href='https://BCLMS.tshwane.gov.za/app-login' class='btn'>Visit Our Partners Page</a>
            </p>

            <p>We will be in touch soon with your official Partner Certificate, branding pack, and next steps to activate your pledge.</p>

            <p style='text-align:center; color:#49A942; font-size:20px; margin-top:50px;'>
                <strong>Together, we are building tomorrow’s leaders — today.</strong>
            </p>

            <p style='text-align:center; margin-top:30px;'>
                Warm regards,<br>
                <strong>The Ithuba Team</strong><br>
                City of Tshwane Economic Development Division
            </p>
        </div>

        <!-- Footer -->
        <div class='footer'>
            <p><strong>City of Tshwane • Economic Development Division</strong><br>
            6th Floor, Middestad Building, 252 Thabo Sehume Street, Pretoria, 0002<br>
            PO Box 6338, Pretoria, 0001</p>
            <p>Tel: 012 358 1634 / 5700 / 5587 | Email: <a href='mailto:Ithuba@TSHWANE.GOV.ZA'>Ithuba@TSHWANE.GOV.ZA</a></p>
            <p>© {DateTime.Now.Year} City of Tshwane. All rights reserved.</p>
        </div>
    </div>
</body>
</html>";
    }
    public static string BuildHtmlSuccessy(string Name, string Password)
	{
		return string.Empty;
	}

	public static DataTable ConvertToDatatable<T>(this IList<T> data)
	{
		PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(typeof(T));
		DataTable dataTable = new DataTable();
		for (int i = 0; i < properties.Count; i++)
		{
			PropertyDescriptor propertyDescriptor = properties[i];
			dataTable.Columns.Add(propertyDescriptor.Name, propertyDescriptor.PropertyType);
		}
		object[] array = new object[properties.Count];
		foreach (T datum in data)
		{
			for (int j = 0; j < array.Length; j++)
			{
				array[j] = properties[j].GetValue(datum);
			}
			dataTable.Rows.Add(array);
		}
		return dataTable;
	}

	public static Color GetColourShade(float correctionFactor, Color inputColour)
	{
		Color color = default(Color);
		float num = (float)(255 - inputColour.R) * correctionFactor + (float)(int)inputColour.R;
		if (num >= 245f)
		{
			num -= 265f;
			if (num <= 0f)
			{
				num += 50f;
			}
			num += 1f;
		}
		float num2 = (float)(255 - inputColour.G) * correctionFactor + (float)(int)inputColour.G;
		if (num2 >= 245f)
		{
			num2 -= 265f;
			if (num2 <= 0f)
			{
				num2 += 50f;
			}
			num2 += 1f;
		}
		float num3 = (float)(255 - inputColour.B) * correctionFactor + (float)(int)inputColour.B;
		if (num3 >= 245f)
		{
			num3 -= 266f;
			if (num3 <= 0f)
			{
				num3 += 50f;
			}
			num3 += 1f;
		}
		if ((int)num == 0 && (int)num2 == 0 && (int)num3 == 0)
		{
			num = 10f;
			num2 = 10f;
			num3 = 10f;
		}
		if ((int)num == 255 && (int)num2 == 255 && (int)num3 == 255)
		{
			num = 100f;
			num2 = 100f;
			num3 = 100f;
		}
		return Color.FromArgb(inputColour.A, (int)num, (int)num2, (int)num3);
	}

	public static string GetContentType(string Extension)
	{
		string empty = string.Empty;
		return Extension switch
		{
			"doc" => empty = "application/vnd.ms-word", 
			"docx" => empty = "application/vnd.ms-word", 
			"xls" => empty = "application/vnd.ms-excel", 
			"xlsx" => empty = "application/vnd.ms-excel", 
			"jpg" => empty = "image/jpg", 
			"png" => empty = "image/png", 
			"gif" => empty = "image/gif", 
			"pdf" => empty = "application/pdf", 
			_ => empty, 
		};
	}

	public static string UnescapeXMLValue(string xmlString)
	{
		if (xmlString == null)
		{
			throw new ArgumentNullException("xmlString");
		}
		return xmlString.Replace("&apos;", "'").Replace("&quot;", "\"").Replace("&gt;", ">")
			.Replace("&lt;", "<")
			.Replace("&amp;", "&");
	}

	public static string EscapeXMLValue(string xmlString)
	{
		if (xmlString == null)
		{
			throw new ArgumentNullException("xmlString");
		}
		return xmlString.Replace("'", "&apos;").Replace("\"", "&quot;").Replace(">", "&gt;")
			.Replace("<", "&lt;")
			.Replace("&", "&amp;");
	}

	public static byte[] getPDFBytes()
	{
		return new byte[0];
	}

	public static byte[] DownloadFile(string url)
	{
		try
		{
			HttpWebRequest httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
			httpWebRequest.Credentials = new NetworkCredential("SQL085Admin", "T$hwane123", "Tshwane");
			using HttpWebResponse httpWebResponse = (HttpWebResponse)httpWebRequest.GetResponse();
			using Stream stream = httpWebResponse.GetResponseStream();
			using MemoryStream memoryStream = new MemoryStream();
			stream.CopyTo(memoryStream);
			return memoryStream.ToArray();
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}
}
