import { createContext } from "react";

export interface Account {
  name: string,
  email: string,
  isAdmin: boolean
}

export interface AccountContextType {
  account: Account;
  setAccount: React.Dispatch<React.SetStateAction<Account>>;
}

export const NullAccount: Account = {
  name: "",
  email: "",
  isAdmin: false,
};

const AccountContext = createContext<Account>({
  name: "",
  email: "",
  isAdmin: false,
});

export default AccountContext;