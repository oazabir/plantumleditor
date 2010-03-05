using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using PlantUmlEditor.Model;
using System.Collections.ObjectModel;

namespace PlantUmlEditor.DesignTimeData
{
    public class DiagramFiles : ObservableCollection<DiagramFile>
    {
        public DiagramFiles()
        {
            this.Add(new DiagramFile()
            {
                Content = 
@"@startuml cpe.png
actor EndUser
participant SaaS
participant CPE

EndUser -> SaaS : Click Download
activate SaaS
	SaaS -> CPE: Generate Unique URL
	CPE -> SaaS: Unique URL
	SaaS -> EndUser: Unique URL
deactivate SaaS

EndUser -> CPE : Hit Unique URL
activate CPE
	CPE -> SaaS : Validate Unique URL
	activate SaaS
		SaaS -> SaaS : Mark Unique URL as disposed
		SaaS -> CPE : Actual App Url
	deactivate SaaS
	CPE -> CPE : Read the file from actual URL
	CPE -> EndUser : Transmit actual app binary
deactivate CPE
@enduml",
                DiagramFilePath = "test.txt",
                ImageFilePath = "http://farm1.static.flickr.com/21/24998062_6f4f96ac99.jpg"
            });

            this.Add(new DiagramFile()
            {
                Content = 
@"@startuml btconnectjourney.png
actor User
participant AOOJ
participant SaaS
participant DnP
participant C2B
participant HE

User -> AOOJ: Enters userID 
activate AOOJ
	AOOJ -> DnP: Check for uniqueness
	DnP -> AOOJ: Unique, no problem	
deactivate AOOJ

User -> AOOJ: Submit order
activate AOOJ
	AOOJ -> SaaS: Queue Order
	AOOJ --> User: Show order in progress
deactivate AOOJ

SaaS -> SaaS: Process order
activate SaaS
	SaaS -> DnP: Create user account
	SaaS -> C2B: Create customer account
	SaaS -> HE: Create mailbox
	SaaS -> User: Send welcome email
deactivate SaaS

@enduml",
                DiagramFilePath = "test2.txt",
                ImageFilePath = "http://farm4.static.flickr.com/3336/3296789176_90588ac94a.jpg"
            });
        }
    }
}
