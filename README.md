# Authorize.Net DPM payment gateway integration module
Authorize.Net Direct Post Method (DPM) payment gateway module provides integration with Authorize.Net DPM trough <a href="http://developer.authorize.net/api" target="_blank">Payment Form (SIM)</a> API.

# Installation
Installing the module:
* Automatically: in VC Manager go to Configuration -> Modules -> Authorize.Net DPM payment gateway -> Install
* Manually: download module zip package from https://github.com/VirtoCommerce/vc-module-Authorize.Net/releases. In VC Manager go to Configuration -> Modules -> Advanced -> upload module package -> Install.

# Module management and settings UI
![image](https://cloud.githubusercontent.com/assets/5801549/16419453/79ea54c6-3d56-11e6-82c8-979763c62030.png)

# Settings
* **API login id** - Authorize.Net API login ID from credentials
* **Transaction key** - Authorize.Net transaction key from credentials
* **MD5 hash key** - Authorize Net MD5 hash key used for relay response validation
* **Mode** - Mode of Authorize.Net payment gateway (test or real)
* **Confirmation URL** - URL for payment confirmation in VC Manager API. {VC manager URL}/api/payments/an/registerpayment
* **Thank you page relative URL** - Storefront thank you page relative URL
* **Payment action type** - Action type of payment


# License
Copyright (c) Virto Solutions LTD.  All rights reserved.

Licensed under the Virto Commerce Open Software License (the "License"); you
may not use this file except in compliance with the License. You may
obtain a copy of the License at

http://virtocommerce.com/opensourcelicense

Unless required by applicable law or agreed to in writing, software
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or
implied.
