using Amazon.EC2;
using Amazon.EC2.Model;
using Amazon.Runtime;
using Amazon.Runtime.CredentialManagement;
using System;
using System.Collections.Generic;
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

                var association = await ec2Client.AssociateAddressAsync(new AssociateAddressRequest
                {
                    AllocationId = address.AllocationId,
                    InstanceId = instance.InstanceId
                });

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

                // ssh
                // start chrome

                Console.WriteLine("press a key to shutdown");

                Console.ReadKey();

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

                // kill ssh
                // kill chrome


                Console.WriteLine("shutdown successful");

                Console.ReadKey();
            }
        }
    }
}
