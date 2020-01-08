using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;

namespace AwsVpnActivator
{
    class Program
    {
        static async Task Main(string[] args)
        {
            var credentialProfileStoreChain = new CredentialProfileStoreChain();
            if (!credentialProfileStoreChain.TryGetAWSCredentials("personal", out AWSCredentials defaultCredentials))
                throw new AmazonClientException("Unable to find \"personal\" profile in CredentialProfileStoreChain.");

            using (var ec2Client = new AmazonEC2Client(defaultCredentials))
            {
                var instances = await ec2Client.DescribeInstancesAsync();
                var instance = instances.Reservations.FirstOrDefault()?.Instances.FirstOrDefault(i => i.Tags.Any(t => t.Key == "Name" && t.Value == "vpn"));

                var address = await ec2Client.AllocateAddressAsync();

                Console.WriteLine($"Ip address allocated");

                var association = await ec2Client.AssociateAddressAsync(new AssociateAddressRequest
                {
                    AllocationId = address.AllocationId,
                    InstanceId = instance.InstanceId
                });

                Console.WriteLine($"Ip address associated");

                await ec2Client.StartInstancesAsync(new StartInstancesRequest
                {
                    InstanceIds = new List<string> { instance.InstanceId }
                });

                Console.WriteLine($"Starting the instance on {address.PublicIp}");

                // wait till starts
                var started = false;
                while (!started)
                {
                    await Task.Delay(1000);
                    var instanceStatuses = await ec2Client.DescribeInstanceStatusAsync(new DescribeInstanceStatusRequest
                    {
                        InstanceIds = new List<string> { instance.InstanceId }
                    });
                    if (instanceStatuses.InstanceStatuses.Any())
                        started = instanceStatuses.InstanceStatuses.FirstOrDefault().InstanceState.Code == 16;
                }

                Console.WriteLine($"Started, Connecting via SSH");

                // ssh
                Process sshProcess = new Process();
                sshProcess.StartInfo.FileName = "ssh";
                sshProcess.StartInfo.Arguments = "-D 8080 -i \"C:\\Users\\htolg\\OneDrive\\Belgeler\\htolgaevcimen-vpn-ec2.pem\" ubuntu@" + address.PublicIp;
                sshProcess.StartInfo.RedirectStandardOutput = true;
                sshProcess.Start();

                var connected = false;
                while (!connected)
                {
                    await Task.Delay(50);
                    var output = sshProcess.StandardOutput.ReadLine();
                    if (output.Contains("Last login"))
                    {
                        connected = true;
                    }
                }
                
                Console.WriteLine($"Connected, Starting Chrome");
                
                // start chrome
                var chromeProcess = new Process();
                chromeProcess.StartInfo.FileName = @"C:\Program Files (x86)\Google\Chrome\Application\chrome.exe";
                chromeProcess.StartInfo.Arguments = "--user-data-dir=\"%USERPROFILE%\\proxy-profile\" --proxy-server=\"socks5://localhost:8080\"";
                chromeProcess.Start();

                Console.WriteLine($"Chrome Started");

                Console.WriteLine("\n\npress a key to shutdown");

                Console.ReadKey();

                // kill ssh
                sshProcess.Close();
                // kill chrome
                chromeProcess.Kill();

                await ec2Client.StopInstancesAsync(new StopInstancesRequest
                {
                    InstanceIds = new List<string> { instance.InstanceId },
                    Force = true
                });

                Console.WriteLine("Stopping the instance");

                await ec2Client.DisassociateAddressAsync(new DisassociateAddressRequest
                {
                    AssociationId = association.AssociationId
                });

                await ec2Client.ReleaseAddressAsync(new ReleaseAddressRequest
                {
                    AllocationId = address.AllocationId
                });

                // wait tll stops
                var stopped = false;
                while (!stopped)
                {
                    await Task.Delay(1000);
                    var instanceStatuses = await ec2Client.DescribeInstanceStatusAsync(new DescribeInstanceStatusRequest
                    {
                        InstanceIds = new List<string> { instance.InstanceId }
                    });
                    if (instanceStatuses.InstanceStatuses.Any())
                        started = instanceStatuses.InstanceStatuses.FirstOrDefault().InstanceState.Code == 80;
                    else stopped = true;
                }
                               
                Console.WriteLine("shutdown successful");
                Console.ReadKey();
            }
        }
    }
}
