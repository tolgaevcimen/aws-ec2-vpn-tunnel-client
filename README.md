# aws-ec2-vpn-tunnel-client

Having a simple ec2 instance on aws that you can ssh into, is a way to use vpn tunnels on your end. This project helps you to automate the:

- Allocating an elasticIP
- Associating the elasticIP to EC2 instance
- Starting EC2 instance
- Starting the SSH connection
- Starting Chrome browser with tunnel configuration

And:

- Stopping the SSH connection
- Stopping the EC2 instance
- Disassociating the elasticIP
- Releasing the elasticIP
